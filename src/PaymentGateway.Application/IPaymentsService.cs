using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public interface IPaymentsService
{
    Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken);
}