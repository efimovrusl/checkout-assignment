using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService : IPaymentsService
{
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsService(IPaymentsRepository paymentsRepository)
    {
        _paymentsRepository = paymentsRepository;
    }

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        return _paymentsRepository.GetAsync(id, cancellationToken);
    }
}