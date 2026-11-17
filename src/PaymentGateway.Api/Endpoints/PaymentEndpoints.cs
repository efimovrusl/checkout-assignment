using Microsoft.AspNetCore.Http.HttpResults;

using PaymentGateway.Api.Mappings;
using PaymentGateway.Api.Models.Requests;
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
            group.MapPost("/", ProcessPaymentAsync);

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

    private static async Task<Results<CreatedAtRoute<PostPaymentResponse>, ValidationProblem, ProblemHttpResult>> ProcessPaymentAsync(
        PostPaymentRequest request,
        IPaymentsService paymentsService,
        CancellationToken cancellationToken)
    {
        var result = await paymentsService.ProcessPaymentAsync(request.ToCommand(), cancellationToken);

        return result switch
        {
            ProcessPaymentResult.Processed processed => TypedResults.CreatedAtRoute(
                processed.Payment.ToPostResponse(),
                "GetPayment",
                new { id = processed.Payment.Id }),
            ProcessPaymentResult.Rejected rejected => TypedResults.ValidationProblem(
                rejected.Errors.ToDictionary(e => e.Key, e => e.Value),
                title: "payment rejected"),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "acquiring bank unavailable")
        };
    }
}
