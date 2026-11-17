namespace PaymentGateway.Application;

public sealed record BankAuthorization(bool Authorized, string? AuthorizationCode);
