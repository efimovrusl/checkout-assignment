using Microsoft.Extensions.Logging;

using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService : IPaymentsService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly ProcessPaymentCommandValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<PaymentsService> _logger;

    public PaymentsService(
        IPaymentsRepository paymentsRepository,
        ProcessPaymentCommandValidator validator,
        IBankClient bankClient,
        IIdempotencyStore idempotencyStore,
        ILogger<PaymentsService> logger)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _bankClient = bankClient;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        return _paymentsRepository.GetAsync(id, cancellationToken);
    }

    public async Task<ProcessPaymentResult> ProcessPaymentAsync(
        ProcessPaymentCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(command);

        if (errors.Count > 0)
        {
            return new ProcessPaymentResult.Rejected(errors);
        }

        if (idempotencyKey is not null)
        {
            var claim = await _idempotencyStore.ClaimAsync(
                idempotencyKey,
                IdempotencyFingerprint.Compute(command),
                cancellationToken);

            switch (claim)
            {
                case IdempotencyClaim.Completed completed:
                    var original = await _paymentsRepository.GetAsync(completed.PaymentId, cancellationToken)
                        ?? throw new InvalidOperationException("idempotent payment is missing");

                    return new ProcessPaymentResult.Processed(original, Replayed: true);
                case IdempotencyClaim.InProgress:
                    return new ProcessPaymentResult.Conflict();
                case IdempotencyClaim.Unknown:
                    return new ProcessPaymentResult.OutcomeUnknown();
                case IdempotencyClaim.Mismatch:
                    return new ProcessPaymentResult.KeyReused();
            }
        }

        var cardNumber = command.CardNumber!;
        var bankRequest = new BankPaymentRequest(
            cardNumber,
            command.ExpiryMonth!.Value,
            command.ExpiryYear!.Value,
            command.Currency!,
            command.Amount!.Value,
            command.Cvv!);

        BankAuthorization authorization;

        try
        {
            authorization = await _bankClient.AuthorizeAsync(bankRequest, cancellationToken);
        }
        catch (AcquiringBankException ex)
        {
            if (ex.OutcomeUnknown)
            {
                _logger.LogWarning(ex, "payment outcome unknown");

                if (idempotencyKey is not null)
                {
                    await _idempotencyStore.MarkUnknownAsync(idempotencyKey, cancellationToken);
                }

                return new ProcessPaymentResult.OutcomeUnknown();
            }

            _logger.LogWarning(ex, "acquiring bank unavailable");

            if (idempotencyKey is not null)
            {
                await _idempotencyStore.ReleaseAsync(idempotencyKey, cancellationToken);
            }

            return new ProcessPaymentResult.BankUnavailable();
        }

        var payment = new Payment(
            Guid.CreateVersion7(),
            authorization.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            cardNumber[^4..],
            bankRequest.ExpiryMonth,
            bankRequest.ExpiryYear,
            bankRequest.Currency,
            bankRequest.Amount);

        await _paymentsRepository.AddAsync(payment, cancellationToken);

        if (idempotencyKey is not null)
        {
            await _idempotencyStore.CompleteAsync(idempotencyKey, payment.Id, cancellationToken);
        }

        return new ProcessPaymentResult.Processed(payment);
    }
}
