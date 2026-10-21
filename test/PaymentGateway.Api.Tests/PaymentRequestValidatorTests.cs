using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests;

// These tests check which field is rejected, not the wording of the message. Each invalid input
// breaks exactly one rule and is otherwise valid, so a rejection proves that particular rule works.
public class PaymentRequestValidatorTests
{
    private static readonly DateTimeOffset Today = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptsAValidRequest()
    {
        // Arrange
        var validator = CreateValidator(Today);

        // Act
        var validated = validator.Validate(ValidRequest(), out var errors);

        // Assert
        Assert.Empty(errors);
        var payment = Assert.IsType<ValidatedPaymentRequest>(validated);
        Assert.Equal("2222405343248877", payment.CardNumber);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2027, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
        Assert.Equal("123", payment.Cvv);
    }

    [Fact]
    public void ReportsEveryMissingFieldTogether()
    {
        // Arrange
        var validator = CreateValidator(Today);

        // Act
        var validated = validator.Validate(new PostPaymentRequest(), out var errors);

        // Assert
        Assert.Null(validated);
        Assert.Equal(
            new[] { "amount", "cardNumber", "currency", "cvv", "expiryMonth", "expiryYear" },
            errors.Keys.OrderBy(field => field));
    }

    [Theory]
    [InlineData("22224053432488")]
    [InlineData("2222405343248877123")]
    public void AcceptsCardNumbersAtTheLengthLimits(string cardNumber)
    {
        // Arrange
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("2222405343248")]           // 13 digits
    [InlineData("22224053432488771234")]    // 20 digits
    [InlineData("22224053432488a7")]        // a letter
    [InlineData("2222 4053 4324 8877")]     // spaces
    [InlineData("٢٢٢٢٤٠٥٣٤٣٢٤٨٨٧٧")]        // Arabic-Indic digits
    public void RejectsInvalidCardNumbers(string cardNumber)
    {
        // Arrange
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        // Act
        var errors = Validate(request);

        // Assert
        AssertOnlyFieldRejected("cardNumber", errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RejectsExpiryMonthsOutsideOneToTwelve(int expiryMonth)
    {
        // Arrange
        var request = ValidRequest();
        request.ExpiryMonth = expiryMonth;

        // Act
        var errors = Validate(request);

        // Assert
        AssertOnlyFieldRejected("expiryMonth", errors);
    }

    [Theory]
    [InlineData("2026-10-21", 10, 2026, true)]
    [InlineData("2026-10-21", 11, 2026, true)]
    [InlineData("2026-10-21", 9, 2026, false)]
    [InlineData("2026-10-21", 12, 2025, false)]
    [InlineData("2026-12-31", 1, 2027, true)]
    [InlineData("2027-01-01", 12, 2026, false)]
    public void TreatsTheExpiryMonthAsValidUntilItEnds(string today, int expiryMonth, int expiryYear, bool expectedValid)
    {
        // Arrange
        var now = DateTimeOffset.Parse(today + "T00:00:00Z");
        var request = ValidRequest();
        request.ExpiryMonth = expiryMonth;
        request.ExpiryYear = expiryYear;

        // Act
        var errors = Validate(request, now);

        // Assert
        if (expectedValid)
            Assert.Empty(errors);
        else
            AssertOnlyFieldRejected("expiryYear", errors);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsSupportedCurrencies(string currency)
    {
        // Arrange
        var request = ValidRequest();
        request.Currency = currency;

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPX")]
    [InlineData("JPY")]
    [InlineData("gbp")]
    public void RejectsInvalidCurrencies(string currency)
    {
        // Arrange
        var request = ValidRequest();
        request.Currency = currency;

        // Act
        var errors = Validate(request);

        // Assert
        AssertOnlyFieldRejected("currency", errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RejectsAmountsThatAreNotPositive(int amount)
    {
        // Arrange
        var request = ValidRequest();
        request.Amount = amount;

        // Act
        var errors = Validate(request);

        // Assert
        AssertOnlyFieldRejected("amount", errors);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("012")]
    public void AcceptsValidCvvs(string cvv)
    {
        // Arrange
        var request = ValidRequest();
        request.Cvv = cvv;

        // Act
        var errors = Validate(request);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("12")]       // too short
    [InlineData("12345")]    // too long
    [InlineData("12a")]      // a letter
    public void RejectsInvalidCvvs(string cvv)
    {
        // Arrange
        var request = ValidRequest();
        request.Cvv = cvv;

        // Act
        var errors = Validate(request);

        // Assert
        AssertOnlyFieldRejected("cvv", errors);
    }

    [Fact]
    public void NeverEchoesCardDataInErrorMessages()
    {
        // Arrange
        const string cardNumber = "2222405343248";
        const string cvv = "98a";
        var request = ValidRequest();
        request.CardNumber = cardNumber;
        request.Cvv = cvv;

        // Act
        var errors = Validate(request);

        // Assert
        var messages = errors.Values.SelectMany(fieldErrors => fieldErrors).ToList();
        Assert.NotEmpty(messages);
        Assert.DoesNotContain(messages, message => message.Contains(cardNumber));
        Assert.DoesNotContain(messages, message => message.Contains(cvv));
    }

    private static PostPaymentRequest ValidRequest()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }

    private static PaymentRequestValidator CreateValidator(DateTimeOffset now)
    {
        return new PaymentRequestValidator(new FakeTimeProvider(now));
    }

    private static Dictionary<string, string[]> Validate(PostPaymentRequest request, DateTimeOffset? now = null)
    {
        CreateValidator(now ?? Today).Validate(request, out var errors);

        return errors;
    }

    // Only the given field is rejected, and by exactly one rule.
    private static void AssertOnlyFieldRejected(string field, Dictionary<string, string[]> errors)
    {
        Assert.Equal(new[] { field }, errors.Keys);
        Assert.Single(errors[field]);
    }
}