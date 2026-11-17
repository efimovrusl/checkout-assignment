using Microsoft.AspNetCore.Http.HttpResults;

using PaymentGateway.Api.Mappings;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application;

namespace PaymentGateway.Api.Endpoints;

public static class PaymentEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapPaymentEndpoints()
        {
            var group = app.MapGroup("/api/payments").WithTags("Payments");

            group.MapGet("/{id:guid}", GetPaymentAsync).WithName("GetPayment");

            return app;
        }
    }

    private static async Task<Results<Ok<GetPaymentResponse>, NotFound>> GetPaymentAsync(
        Guid id,
        IPaymentsService paymentsService,
        CancellationToken cancellationToken)
    {
        var payment = await paymentsService.GetPaymentAsync(id, cancellationToken);

        return payment is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(payment.ToResponse());
    }
}