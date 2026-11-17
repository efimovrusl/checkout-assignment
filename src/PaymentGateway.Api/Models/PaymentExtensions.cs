using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Models;

public static class PaymentExtensions
{
    extension(Payment payment)
    {
        public GetPaymentResponse ToResponse() => new()
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };
    }
}
