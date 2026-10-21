using System.Text.Json.Serialization;

namespace PaymentGateway.Api.AcquiringBank;

internal class BankPaymentResponse
{
    // Nullable on purpose: a response without this field must not be read as a decline.
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; set; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; set; }
}