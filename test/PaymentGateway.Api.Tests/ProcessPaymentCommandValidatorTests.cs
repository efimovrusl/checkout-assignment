using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Application;

namespace PaymentGateway.Api.Tests;

public class ProcessPaymentCommandValidatorTests
{
    private readonly ProcessPaymentCommandValidator _validator = new(
        new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)));

    private static ProcessPaymentCommand ValidCommand() => new(
        "12345678901234",
        12,
        2030,
        "GBP",
        100,
        "123");

    [Fact]
    public void Validate_ReturnsNoErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = ValidCommand();

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReportsEveryField_WhenCommandIsEmpty()
    {
        // Arrange
        var command = new ProcessPaymentCommand(null, null, null, null, null, null);

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Equal(6, errors.Count);
        foreach (var field in new[] { "cardNumber", "expiryMonth", "expiryYear", "currency", "amount", "cvv" })
        {
            Assert.Equal(["is required"], errors[field]);
        }
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234567890123a")]
    public void Validate_RejectsCardNumber_WhenLengthOrCharactersAreInvalid(string cardNumber)
    {
        // Arrange
        var command = ValidCommand() with { CardNumber = cardNumber };

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Equal(["must be 14 to 19 digits"], errors["cardNumber"]);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    public void Validate_RejectsCvv_WhenLengthOrCharactersAreInvalid(string cvv)
    {
        // Arrange
        var command = ValidCommand() with { Cvv = cvv };

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Equal(["must be 3 or 4 digits"], errors["cvv"]);
    }

    [Theory]
    [InlineData(10, 2026, false)]
    [InlineData(11, 2026, true)]
    public void Validate_ChecksExpiryAgainstCurrentMonth(int month, int year, bool isValid)
    {
        // Arrange
        var command = ValidCommand() with { ExpiryMonth = month, ExpiryYear = year };

        // Act
        var errors = _validator.Validate(command);

        // Assert
        if (isValid)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Equal(["must be in the future"], errors["expiry"]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_RejectsExpiryMonth_WhenOutsideRange(int month)
    {
        // Arrange
        var command = ValidCommand() with { ExpiryMonth = month };

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Equal(["must be between 1 and 12"], errors["expiryMonth"]);
    }

    [Theory]
    [InlineData("gbp", 100, "currency", "must be one of GBP, USD, EUR")]
    [InlineData("JPY", 100, "currency", "must be one of GBP, USD, EUR")]
    [InlineData("GBP", 0, "amount", "must be greater than 0")]
    public void Validate_RejectsCurrencyOrAmount_WhenInvalid(string currency, long amount, string field, string message)
    {
        // Arrange
        var command = ValidCommand() with { Currency = currency, Amount = amount };

        // Act
        var errors = _validator.Validate(command);

        // Assert
        Assert.Equal([message], errors[field]);
    }
}
