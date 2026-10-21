namespace PaymentGateway.Api.Models.Requests;

/// <summary>
/// The payment request as sent by the merchant. Every field is nullable so that missing values
/// reach the validator and are reported together, instead of failing during deserialization.
/// </summary>
/// <remarks>
/// A class rather than a record on purpose: a record's generated ToString prints every property,
/// which would leak the card number and CVV into logs.
/// </remarks>
public class PostPaymentRequest
{
    public string? CardNumber { get; set; }
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public string? Currency { get; set; }
    public int? Amount { get; set; }
    public string? Cvv { get; set; }
}