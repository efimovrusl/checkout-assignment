using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Application;

namespace PaymentGateway.Api.Mappings;

public static class PostPaymentRequestMappings
{
    extension(PostPaymentRequest request)
    {
        public ProcessPaymentCommand ToCommand() => new(
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount,
            request.Cvv);
    }
}
