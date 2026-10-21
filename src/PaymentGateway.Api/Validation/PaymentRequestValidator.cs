using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

/// <summary>
/// Checks a merchant's payment request against the gateway's rules. Every failing field is
/// reported in one go. Messages never include the submitted values, so card data cannot leak
/// through error responses.
/// </summary>
public class PaymentRequestValidator
{
    private static readonly string[] SupportedCurrencies = new[] { "GBP", "USD", "EUR" };

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the validated request, or null when the request is invalid. In that case
    /// <paramref name="errors"/> lists the problems per field.
    /// </summary>
    public ValidatedPaymentRequest? Validate(PostPaymentRequest request, out Dictionary<string, string[]> errors)
    {
        var failures = new Dictionary<string, List<string>>();

        // Each check returns the value when it is valid, or null after recording why it is not.
        var cardNumber = ValidateNumericText(request.CardNumber, "cardNumber", "Card number", 14, 19, failures);
        var expiryMonth = ValidateExpiryMonth(request.ExpiryMonth, failures);
        var expiryYear = ValidateExpiryYear(request.ExpiryYear, expiryMonth, failures);
        var currency = ValidateCurrency(request.Currency, failures);
        var amount = ValidateAmount(request.Amount, failures);
        var cvv = ValidateNumericText(request.Cvv, "cvv", "CVV", 3, 4, failures);

        errors = failures.ToDictionary(failure => failure.Key, failure => failure.Value.ToArray());

        if (cardNumber == null || expiryMonth == null || expiryYear == null
            || currency == null || amount == null || cvv == null)
            return null;

        return new ValidatedPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth.Value,
            ExpiryYear = expiryYear.Value,
            Currency = currency,
            Amount = amount.Value,
            Cvv = cvv
        };
    }

    private static string? ValidateNumericText(
        string? value,
        string field,
        string label,
        int minLength,
        int maxLength,
        Dictionary<string, List<string>> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(failures, field, $"{label} is required.");
            return null;
        }

        var isValid = true;

        if (value.Length < minLength || value.Length > maxLength)
        {
            AddError(failures, field, $"{label} must be between {minLength} and {maxLength} characters long.");
            isValid = false;
        }

        // IsAsciiDigit rather than IsDigit: IsDigit also accepts digits from other scripts, such as Arabic-Indic.
        if (!value.All(char.IsAsciiDigit))
        {
            AddError(failures, field, $"{label} must only contain numeric characters.");
            isValid = false;
        }

        return isValid ? value : null;
    }

    private static int? ValidateExpiryMonth(int? month, Dictionary<string, List<string>> failures)
    {
        if (month == null)
        {
            AddError(failures, "expiryMonth", "Expiry month is required.");
            return null;
        }

        if (month < 1 || month > 12)
        {
            AddError(failures, "expiryMonth", "Expiry month must be between 1 and 12.");
            return null;
        }

        return month;
    }

    private int? ValidateExpiryYear(int? year, int? validMonth, Dictionary<string, List<string>> failures)
    {
        if (year == null)
        {
            AddError(failures, "expiryYear", "Expiry year is required.");
            return null;
        }

        // Without a valid month the expiry date cannot be checked, and the month error is already reported.
        if (validMonth == null)
            return year;

        if (IsExpired(validMonth.Value, year.Value))
        {
            AddError(failures, "expiryYear", "Expiry date must be in the future.");
            return null;
        }

        return year;
    }

    private bool IsExpired(int month, int year)
    {
        // A card stays valid until the end of its expiry month, so the current month still counts.
        var now = _timeProvider.GetUtcNow();

        return year < now.Year || (year == now.Year && month < now.Month);
    }

    private static string? ValidateCurrency(string? currency, Dictionary<string, List<string>> failures)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            AddError(failures, "currency", "Currency is required.");
            return null;
        }

        if (currency.Length != 3)
        {
            AddError(failures, "currency", "Currency must be 3 characters long.");
            return null;
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            AddError(failures, "currency", $"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.");
            return null;
        }

        return currency;
    }

    private static int? ValidateAmount(int? amount, Dictionary<string, List<string>> failures)
    {
        if (amount == null)
        {
            AddError(failures, "amount", "Amount is required.");
            return null;
        }

        if (amount <= 0)
        {
            AddError(failures, "amount", "Amount must be greater than zero.");
            return null;
        }

        return amount;
    }

    private static void AddError(Dictionary<string, List<string>> failures, string field, string message)
    {
        if (!failures.ContainsKey(field))
            failures[field] = new List<string>();

        failures[field].Add(message);
    }
}