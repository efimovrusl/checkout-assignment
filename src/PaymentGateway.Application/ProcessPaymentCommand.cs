namespace PaymentGateway.Application;

public sealed record ProcessPaymentCommand(
    string? CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? Currency,
    long? Amount,
    string? Cvv);
