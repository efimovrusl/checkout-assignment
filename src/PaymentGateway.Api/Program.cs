using System.Text.Json.Serialization;

using PaymentGateway.Api.Endpoints;
using PaymentGateway.Application;
using PaymentGateway.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest)
        {
            context.ProblemDetails.Extensions.TryAdd("paymentStatus", "Rejected");
        }
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication().AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.MapPaymentEndpoints();

app.Run();