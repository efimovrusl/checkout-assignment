using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Application;

namespace PaymentGateway.Api.Tests;

public class ProcessPaymentEndpointTests
{
    private static readonly object ValidRequest = new
    {
        cardNumber = "2222405343248877",
        expiryMonth = 4,
        expiryYear = DateTime.UtcNow.Year + 1,
        currency = "GBP",
        amount = 100,
        cvv = "123"
    };

    [Theory]
    [InlineData(true, "Authorized")]
    [InlineData(false, "Declined")]
    public async Task CreatesPaymentFromBankAnswer(bool authorized, string expectedStatus)
    {
        // Arrange
        var bank = new FakeBankClient(new BankAuthorization(authorized, authorized ? "code" : null));
        var client = CreateClient(bank);

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var location = response.Headers.Location;
        var getResponse = await client.GetAsync(location);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(location);
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetString());
        Assert.Equal("8877", json.RootElement.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Theory]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2999,"currency":"GBP","amount":100}""")]
    [InlineData("""{"cardNumber":""")]
    public async Task RejectsInvalidRequestWithoutCallingBank(string body)
    {
        // Arrange
        var bank = new FakeBankClient(new BankAuthorization(true, "code"));
        var client = CreateClient(bank);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/payments", content);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Rejected", json.RootElement.GetProperty("paymentStatus").GetString());
        Assert.False(bank.WasCalled);
    }

    [Fact]
    public async Task Returns502WhenBankIsUnavailable()
    {
        // Arrange
        var bank = new FakeBankClient(new AcquiringBankException("down"));
        var client = CreateClient(bank);

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", ValidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private static HttpClient CreateClient(FakeBankClient bank)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services => services.AddSingleton<IBankClient>(bank)))
            .CreateClient();
    }

    private sealed class FakeBankClient : IBankClient
    {
        private readonly BankAuthorization? _answer;
        private readonly Exception? _failure;

        public FakeBankClient(BankAuthorization answer) => _answer = answer;

        public FakeBankClient(Exception failure) => _failure = failure;

        public bool WasCalled { get; private set; }

        public Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;

            return _failure is null ? Task.FromResult(_answer!) : Task.FromException<BankAuthorization>(_failure);
        }
    }
}
