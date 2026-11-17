using System.Net;
using System.Text;
using System.Text.Json;

using PaymentGateway.Application;
using PaymentGateway.Infrastructure.Bank;

namespace PaymentGateway.Api.Tests;

public class HttpBankClientTests
{
    private static readonly Uri BaseAddress = new("http://bank.test/");

    private static readonly BankPaymentRequest Request =
        new("2222405343248877", 4, 2027, "GBP", 100, "123");

    [Fact]
    public async Task PostsSnakeCaseBodyToPaymentsEndpoint()
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""")));
        var client = CreateClient(handler);

        // Act
        await client.AuthorizeAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("http://bank.test/payments"), handler.RequestUri);
        using var expected = JsonDocument.Parse("""{"card_number":"2222405343248877","expiry_date":"04/2027","currency":"GBP","amount":100,"cvv":"123"}""");
        using var actual = JsonDocument.Parse(handler.Body!);
        Assert.Equal(expected.RootElement.GetRawText(), actual.RootElement.GetRawText());
    }

    [Fact]
    public async Task MapsAuthorizedResponseWithAuthorizationCode()
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""")));
        var client = CreateClient(handler);

        // Act
        var result = await client.AuthorizeAsync(Request, CancellationToken.None);

        // Assert
        Assert.True(result.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", result.AuthorizationCode);
    }

    [Fact]
    public async Task MapsDeclinedResponseToNullAuthorizationCode()
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, """{"authorized":false,"authorization_code":""}""")));
        var client = CreateClient(handler);

        // Act
        var result = await client.AuthorizeAsync(Request, CancellationToken.None);

        // Assert
        Assert.False(result.Authorized);
        Assert.Null(result.AuthorizationCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ThrowsAcquiringBankExceptionWhenBankRespondsWithNonSuccessStatus(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));
        var client = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Request, CancellationToken.None));

        // Assert
        Assert.False(exception.OutcomeUnknown);
        Assert.Contains(((int)statusCode).ToString(), exception.Message);
        Assert.DoesNotContain(Request.CardNumber, exception.Message);
        Assert.DoesNotContain(Request.Cvv, exception.Message);
    }

    [Fact]
    public async Task ThrowsAcquiringBankExceptionWhenBankIsUnreachable()
    {
        // Arrange
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Request, CancellationToken.None));

        // Assert
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task ThrowsAcquiringBankExceptionWhenBankTimesOut()
    {
        // Arrange
        var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");
        });
        var client = CreateClient(handler, TimeSpan.FromMilliseconds(100));

        // Act
        var act = () => client.AuthorizeAsync(Request, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(act);

        // Assert
        Assert.True(exception.OutcomeUnknown);
    }

    [Fact]
    public async Task PropagatesCancellationWhenCallerCancels()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(async (_, token) =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");
        });
        var client = CreateClient(handler);

        // Act
        var act = () => client.AuthorizeAsync(Request, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("")]
    public async Task ThrowsAcquiringBankExceptionWhenBodyIsInvalid(string body)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));
        var client = CreateClient(handler);

        // Act
        var act = () => client.AuthorizeAsync(Request, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(act);

        // Assert
        Assert.True(exception.OutcomeUnknown);
    }

    private static HttpBankClient CreateClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = BaseAddress,
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        };

        return new HttpBankClient(httpClient);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return await respond(request, cancellationToken);
        }
    }
}
