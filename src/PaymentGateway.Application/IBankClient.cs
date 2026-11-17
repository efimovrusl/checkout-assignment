namespace PaymentGateway.Application;

public interface IBankClient
{
    Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}
