using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.AcquiringBank;

public class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}