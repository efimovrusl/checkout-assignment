using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Application;

namespace PaymentGateway.Infrastructure.Bank;

internal sealed class HttpBankClient(HttpClient httpClient) : IBankClient
{
    public async Task<BankAuthorization> AuthorizeAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var body = new BankRequestBody(
            request.CardNumber,
            string.Create(CultureInfo.InvariantCulture, $"{request.ExpiryMonth:00}/{request.ExpiryYear:0000}"),
            request.Currency,
            request.Amount,
            request.Cvv);

        try
        {
            using var response = await httpClient.PostAsJsonAsync("payments", body, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AcquiringBankException($"The acquiring bank responded with status code {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<BankResponseBody>(cancellationToken);

            if (result is null)
            {
                throw new AcquiringBankException("The acquiring bank returned an empty response.");
            }

            var code = string.IsNullOrEmpty(result.AuthorizationCode) ? null : result.AuthorizationCode;

            return new BankAuthorization(result.Authorized, code);
        }
        catch (HttpRequestException exception)
        {
            throw new AcquiringBankException("The acquiring bank could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            throw new AcquiringBankException("The acquiring bank returned an unreadable response.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AcquiringBankException("The acquiring bank did not respond in time.", exception);
        }
    }

    internal sealed record BankRequestBody(
        [property: JsonPropertyName("card_number")] string CardNumber,
        [property: JsonPropertyName("expiry_date")] string ExpiryDate,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("cvv")] string Cvv);

    internal sealed record BankResponseBody(
        [property: JsonPropertyName("authorized")] bool Authorized,
        [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);
}
