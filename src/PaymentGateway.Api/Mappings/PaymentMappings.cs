using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Domain;

namespace PaymentGateway.Api.Mappings;

public static class PaymentMappings
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