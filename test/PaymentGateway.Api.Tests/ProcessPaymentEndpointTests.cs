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

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task RetriesWithSameKeyCallBankOnlyWhenOutcomeIsKnown(bool outcomeUnknown, int expectedBankCalls)
    {
        // Arrange
        var bank = new FakeBankClient(new AcquiringBankException("down", outcomeUnknown));
        var client = CreateClient(bank);

        // Act
        var first = await PostWithKeyAsync(client, "key-3", ValidRequest);
        var second = await PostWithKeyAsync(client, "key-3", ValidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, second.StatusCode);
        Assert.Equal(expectedBankCalls, bank.CallCount);
    }

    [Fact]
    public async Task ReplaysOriginalPaymentForSameIdempotencyKeyAndBody()
    {
        // Arrange
        var bank = new FakeBankClient(new BankAuthorization(true, "code"));
        var client = CreateClient(bank);

        // Act
        var first = await PostWithKeyAsync(client, "key-1", ValidRequest);
        var second = await PostWithKeyAsync(client, "key-1", ValidRequest);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstJson.RootElement.GetProperty("id").GetGuid(), secondJson.RootElement.GetProperty("id").GetGuid());
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(1, bank.CallCount);
    }

    [Fact]
    public async Task Returns422WhenIdempotencyKeyIsReusedWithDifferentBody()
    {
        // Arrange
        var bank = new FakeBankClient(new BankAuthorization(true, "code"));
        var client = CreateClient(bank);
        var otherRequest = new
        {
            cardNumber = "2222405343248877",
            expiryMonth = 4,
            expiryYear = DateTime.UtcNow.Year + 1,
            currency = "GBP",
            amount = 200,
            cvv = "123"
        };

        // Act
        await PostWithKeyAsync(client, "key-2", ValidRequest);
        var response = await PostWithKeyAsync(client, "key-2", otherRequest);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(1, bank.CallCount);
    }

    private static Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);

        return client.SendAsync(request);
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

        public int CallCount { get; private set; }

        public bool WasCalled => CallCount > 0;

        public Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
        {
            CallCount++;

            return _failure is null ? Task.FromResult(_answer!) : Task.FromException<BankAuthorization>(_failure);
        }
    }
}
