namespace PaymentGateway.Application;

public class AcquiringBankException : Exception
{
    public AcquiringBankException(string message)
        : base(message)
    {
    }

    public AcquiringBankException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
