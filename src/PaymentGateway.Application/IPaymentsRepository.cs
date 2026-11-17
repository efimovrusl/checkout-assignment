using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public interface IPaymentsRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);
}