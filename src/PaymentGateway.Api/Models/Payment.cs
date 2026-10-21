namespace PaymentGateway.Api.Models;

/// <summary>
/// A payment as stored by the gateway. Only the last four card digits are kept:
/// the full card number and the CVV are never persisted.
/// </summary>
public record Payment
{
    public required Guid Id { get; init; }
    public required PaymentStatus Status { get; init; }
    public required string CardNumberLastFour { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public required int Amount { get; init; }
}