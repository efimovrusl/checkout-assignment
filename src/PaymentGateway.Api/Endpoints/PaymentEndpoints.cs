using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Mappings;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application;

namespace PaymentGateway.Api.Endpoints;

public static class PaymentEndpoints
{
    private const int MaxIdempotencyKeyLength = 64;

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
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpResponse httpResponse,
        IPaymentsService paymentsService,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is { Length: > MaxIdempotencyKeyLength })
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["idempotencyKey"] = [$"must be at most {MaxIdempotencyKeyLength} characters"]
                },
                title: "payment rejected");
        }

        var result = await paymentsService.ProcessPaymentAsync(request.ToCommand(), idempotencyKey, cancellationToken);

        if (result is ProcessPaymentResult.Processed { Replayed: true })
        {
            httpResponse.Headers["Idempotent-Replayed"] = "true";
        }

        return result switch
        {
            ProcessPaymentResult.Processed processed => TypedResults.CreatedAtRoute(
                processed.Payment.ToPostResponse(),
                "GetPayment",
                new { id = processed.Payment.Id }),
            ProcessPaymentResult.Conflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "a request with this idempotency key is in progress"),
            ProcessPaymentResult.OutcomeUnknown => TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "payment outcome unknown, the card may have been charged; do not retry with a new key"),
            ProcessPaymentResult.KeyReused => TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "idempotency key reused with a different request"),
            ProcessPaymentResult.Rejected rejected => TypedResults.ValidationProblem(
                rejected.Errors.ToDictionary(e => e.Key, e => e.Value),
                title: "payment rejected"),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "acquiring bank unavailable")
        };
    }
}
