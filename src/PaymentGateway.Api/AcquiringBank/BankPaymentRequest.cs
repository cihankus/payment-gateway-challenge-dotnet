using System.Text.Json.Serialization;

namespace PaymentGateway.Api.AcquiringBank;

/// <summary>
/// The request body the bank expects. A class rather than a record so that its ToString
/// does not print the card number or CVV.
/// </summary>
internal class BankPaymentRequest
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }

    /// <summary>Formatted as MM/YYYY, for example 04/2027.</summary>
    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public required string Cvv { get; init; }
}