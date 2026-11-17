using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public abstract record ProcessPaymentResult
{
    private ProcessPaymentResult()
    {
    }

    public sealed record Processed(Payment Payment, bool Replayed = false) : ProcessPaymentResult;

    public sealed record Rejected(IReadOnlyDictionary<string, string[]> Errors) : ProcessPaymentResult;

    public sealed record BankUnavailable : ProcessPaymentResult;

    public sealed record OutcomeUnknown : ProcessPaymentResult;

    public sealed record Conflict : ProcessPaymentResult;

    public sealed record KeyReused : ProcessPaymentResult;
}
