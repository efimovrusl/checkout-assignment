using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Application;

internal static class IdempotencyFingerprint
{
    public static string Compute(ProcessPaymentCommand command)
    {
        var lastFour = command.CardNumber![^4..];
        var material = $"{lastFour}|{command.ExpiryMonth}|{command.ExpiryYear}|{command.Currency}|{command.Amount}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
