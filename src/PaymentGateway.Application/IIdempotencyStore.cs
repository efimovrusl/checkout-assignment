namespace PaymentGateway.Application;

public interface IIdempotencyStore
{
    Task<IdempotencyClaim> ClaimAsync(string key, string fingerprint, CancellationToken cancellationToken);

    Task CompleteAsync(string key, Guid paymentId, CancellationToken cancellationToken);

    Task MarkUnknownAsync(string key, CancellationToken cancellationToken);

    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}
