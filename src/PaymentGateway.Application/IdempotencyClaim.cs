namespace PaymentGateway.Application;

public abstract record IdempotencyClaim
{
    private IdempotencyClaim()
    {
    }

    public sealed record Claimed : IdempotencyClaim;

    public sealed record Completed(Guid PaymentId) : IdempotencyClaim;

    public sealed record InProgress : IdempotencyClaim;

    public sealed record Unknown : IdempotencyClaim;

    public sealed record Mismatch : IdempotencyClaim;
}
