namespace PaymentGateway.Application;

public sealed class ProcessPaymentCommandValidator(TimeProvider timeProvider)
{
    private static readonly string[] SupportedCurrencies = ["GBP", "USD", "EUR"];

    public IReadOnlyDictionary<string, string[]> Validate(ProcessPaymentCommand command)
    {
        var errors = new Dictionary<string, List<string>>();

        ValidateCardNumber(command.CardNumber, errors);
        ValidateExpiryMonth(command.ExpiryMonth, errors);
        ValidateExpiryYear(command.ExpiryYear, errors);
        ValidateExpiryDate(command.ExpiryMonth, command.ExpiryYear, errors);
        ValidateCurrency(command.Currency, errors);
        ValidateAmount(command.Amount, errors);
        ValidateCvv(command.Cvv, errors);

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private static void ValidateCardNumber(string? cardNumber, Dictionary<string, List<string>> errors)
    {
        if (cardNumber is null)
        {
            Add(errors, "cardNumber", "is required");
            return;
        }

        if (cardNumber.Length is < 14 or > 19 || !AllAsciiDigits(cardNumber))
        {
            Add(errors, "cardNumber", "must be 14 to 19 digits");
        }
    }

    private static void ValidateExpiryMonth(int? month, Dictionary<string, List<string>> errors)
    {
        if (month is null)
        {
            Add(errors, "expiryMonth", "is required");
            return;
        }

        if (month is < 1 or > 12)
        {
            Add(errors, "expiryMonth", "must be between 1 and 12");
        }
    }

    private static void ValidateExpiryYear(int? year, Dictionary<string, List<string>> errors)
    {
        if (year is null)
        {
            Add(errors, "expiryYear", "is required");
        }
    }

    private void ValidateExpiryDate(int? month, int? year, Dictionary<string, List<string>> errors)
    {
        if (month is null or < 1 or > 12 || year is null)
        {
            return;
        }

        var today = timeProvider.GetUtcNow();
        var isPast = year < today.Year || (year == today.Year && month < today.Month);

        if (isPast)
        {
            Add(errors, "expiry", "must be in the future");
        }
    }

    private static void ValidateCurrency(string? currency, Dictionary<string, List<string>> errors)
    {
        if (currency is null)
        {
            Add(errors, "currency", "is required");
            return;
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            Add(errors, "currency", "must be one of GBP, USD, EUR");
        }
    }

    private static void ValidateAmount(long? amount, Dictionary<string, List<string>> errors)
    {
        if (amount is null)
        {
            Add(errors, "amount", "is required");
            return;
        }

        if (amount <= 0)
        {
            Add(errors, "amount", "must be greater than 0");
        }
    }

    private static void ValidateCvv(string? cvv, Dictionary<string, List<string>> errors)
    {
        if (cvv is null)
        {
            Add(errors, "cvv", "is required");
            return;
        }

        if (cvv.Length is < 3 or > 4 || !AllAsciiDigits(cvv))
        {
            Add(errors, "cvv", "must be 3 or 4 digits");
        }
    }

    private static bool AllAsciiDigits(string value) => value.All(char.IsAsciiDigit);

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }
}
