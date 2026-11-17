using System.Collections.Concurrent;

using PaymentGateway.Application;

namespace PaymentGateway.Infrastructure;

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public Task<IdempotencyClaim> ClaimAsync(string key, string fingerprint, CancellationToken cancellationToken)
    {
        var added = new Entry(fingerprint, null, false);

        if (_entries.TryAdd(key, added))
        {
            return Task.FromResult<IdempotencyClaim>(new IdempotencyClaim.Claimed());
        }

        var existing = _entries[key];

        IdempotencyClaim claim = existing.Fingerprint != fingerprint
            ? new IdempotencyClaim.Mismatch()
            : existing.PaymentId is { } paymentId
                ? new IdempotencyClaim.Completed(paymentId)
                : existing.Unknown
                    ? new IdempotencyClaim.Unknown()
                    : new IdempotencyClaim.InProgress();

        return Task.FromResult(claim);
    }

    public Task CompleteAsync(string key, Guid paymentId, CancellationToken cancellationToken)
    {
        _entries.AddOrUpdate(
            key,
            _ => new Entry(string.Empty, paymentId, false),
            (_, entry) => entry with { PaymentId = paymentId });

        return Task.CompletedTask;
    }

    public Task MarkUnknownAsync(string key, CancellationToken cancellationToken)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = entry with { Unknown = true };
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        _entries.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    private sealed record Entry(string Fingerprint, Guid? PaymentId, bool Unknown);
}
