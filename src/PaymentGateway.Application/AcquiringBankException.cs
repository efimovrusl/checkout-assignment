namespace PaymentGateway.Application;

public class AcquiringBankException : Exception
{
    public AcquiringBankException(string message, bool outcomeUnknown = false)
        : base(message)
    {
        OutcomeUnknown = outcomeUnknown;
    }

    public AcquiringBankException(string message, Exception? innerException, bool outcomeUnknown = false)
        : base(message, innerException)
    {
        OutcomeUnknown = outcomeUnknown;
    }

    public bool OutcomeUnknown { get; }
}
