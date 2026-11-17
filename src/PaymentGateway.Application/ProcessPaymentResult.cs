using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public abstract record ProcessPaymentResult
{
    private ProcessPaymentResult()
    {
    }

    public sealed record Processed(Payment Payment) : ProcessPaymentResult;

    public sealed record Rejected(IReadOnlyDictionary<string, string[]> Errors) : ProcessPaymentResult;

    public sealed record BankUnavailable : ProcessPaymentResult;
}
