namespace PaymentGateway.Infrastructure.Bank;

public sealed class BankOptions
{
    public const string SectionName = "Bank";

    public Uri? BaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}
