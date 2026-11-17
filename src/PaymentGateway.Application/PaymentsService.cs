using Microsoft.Extensions.Logging;

using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService(
    IPaymentsRepository paymentsRepository,
    ProcessPaymentCommandValidator validator,
    IBankClient bankClient,
    IIdempotencyStore idempotencyStore,
    ILogger<PaymentsService> logger) : IPaymentsService
{
    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        return paymentsRepository.GetAsync(id, cancellationToken);
    }

    public async Task<ProcessPaymentResult> ProcessPaymentAsync(
        ProcessPaymentCommand command,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var errors = validator.Validate(command);

        if (errors.Count > 0)
        {
            return new ProcessPaymentResult.Rejected(errors);
        }

        if (idempotencyKey is not null)
        {
            var claim = await idempotencyStore.ClaimAsync(
                idempotencyKey,
                IdempotencyFingerprint.Compute(command),
                cancellationToken);

            switch (claim)
            {
                case IdempotencyClaim.Completed completed:
                    var original = await paymentsRepository.GetAsync(completed.PaymentId, cancellationToken)
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
            authorization = await bankClient.AuthorizeAsync(bankRequest, cancellationToken);
        }
        catch (AcquiringBankException ex)
        {
            if (ex.OutcomeUnknown)
            {
                logger.LogWarning(ex, "payment outcome unknown");

                if (idempotencyKey is not null)
                {
                    await idempotencyStore.MarkUnknownAsync(idempotencyKey, cancellationToken);
                }

                return new ProcessPaymentResult.OutcomeUnknown();
            }

            logger.LogWarning(ex, "acquiring bank unavailable");

            if (idempotencyKey is not null)
            {
                await idempotencyStore.ReleaseAsync(idempotencyKey, cancellationToken);
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

        await paymentsRepository.AddAsync(payment, cancellationToken);

        if (idempotencyKey is not null)
        {
            await idempotencyStore.CompleteAsync(idempotencyKey, payment.Id, cancellationToken);
        }

        return new ProcessPaymentResult.Processed(payment);
    }
}
