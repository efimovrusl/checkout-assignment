namespace PaymentGateway.Application;

public sealed record BankPaymentRequest(string CardNumber, int ExpiryMonth, int ExpiryYear, string Currency, long Amount, string Cvv);
