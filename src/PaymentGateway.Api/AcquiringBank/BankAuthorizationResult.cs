namespace PaymentGateway.Api.AcquiringBank;

public record BankAuthorizationResult
{
    public required bool Authorized { get; init; }

    /// <summary>Issued by the bank for authorised payments. Empty or null when declined.</summary>
    public string? AuthorizationCode { get; init; }
}