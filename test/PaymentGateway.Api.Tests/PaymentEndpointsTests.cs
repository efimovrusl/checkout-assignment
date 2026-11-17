using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application;
using PaymentGateway.Domain;
using PaymentGateway.Infrastructure;

namespace PaymentGateway.Api.Tests;

public class PaymentEndpointsTests
{
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    
    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment(
            Guid.NewGuid(),
            PaymentStatus.Authorized,
            _random.Next(1111, 9999).ToString(),
            _random.Next(1, 12),
            _random.Next(2023, 2030),
            "GBP",
            _random.Next(1, 10000));

        var paymentsRepository = new InMemoryPaymentsRepository();
        await paymentsRepository.AddAsync(payment, CancellationToken.None);

        var webApplicationFactory = new WebApplicationFactory<Program>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services
                .AddSingleton<IPaymentsRepository>(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse.Id);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task SerializesStatusAndCardNumberLastFourAsStrings()
    {
        // Arrange
        var payment = new Payment(
            Guid.NewGuid(),
            PaymentStatus.Authorized,
            "0042",
            12,
            2030,
            "GBP",
            100);

        var paymentsRepository = new InMemoryPaymentsRepository();
        await paymentsRepository.AddAsync(payment, CancellationToken.None);

        var webApplicationFactory = new WebApplicationFactory<Program>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services
                .AddSingleton<IPaymentsRepository>(paymentsRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{payment.Id}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("status").ValueKind);
        Assert.Equal("Authorized", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("cardNumberLastFour").ValueKind);
        Assert.Equal("0042", json.RootElement.GetProperty("cardNumberLastFour").GetString());
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<Program>();
        var client = webApplicationFactory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}