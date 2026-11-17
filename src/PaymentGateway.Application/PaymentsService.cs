using Microsoft.Extensions.Logging;

using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService : IPaymentsService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly ProcessPaymentCommandValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly ILogger<PaymentsService> _logger;

    public PaymentsService(
        IPaymentsRepository paymentsRepository,
        ProcessPaymentCommandValidator validator,
        IBankClient bankClient,
        ILogger<PaymentsService> logger)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _bankClient = bankClient;
        _logger = logger;
    }

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        return _paymentsRepository.GetAsync(id, cancellationToken);
    }

    public async Task<ProcessPaymentResult> ProcessPaymentAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(command);

        if (errors.Count > 0)
        {
            return new ProcessPaymentResult.Rejected(errors);
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
            _logger.LogWarning(ex, "acquiring bank unavailable");

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

        return new ProcessPaymentResult.Processed(payment);
    }
}
