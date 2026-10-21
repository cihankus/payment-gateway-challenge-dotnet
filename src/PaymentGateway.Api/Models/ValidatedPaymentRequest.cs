namespace PaymentGateway.Api.Models;

/// <summary>
/// A payment request that has passed validation, produced by the payment request validator.
/// Not a record, so its ToString does not print the card number or CVV.
/// </summary>
public class ValidatedPaymentRequest
{
    public required string CardNumber { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public required int Amount { get; init; }
    public required string Cvv { get; init; }

    public string CardNumberLastFour => CardNumber.Substring(CardNumber.Length - 4);
}