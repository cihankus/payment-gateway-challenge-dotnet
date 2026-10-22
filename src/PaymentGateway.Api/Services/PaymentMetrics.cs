using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Services;

/// <summary>
/// Counts payment requests by outcome: authorized, declined, rejected or bank_unavailable.
/// A rise in rejections can point to a broken merchant integration or card testing;
/// a rise in bank_unavailable points to a problem with the acquiring bank.
/// </summary>
public class PaymentMetrics
{
    public const string MeterName = "PaymentGateway";

    private readonly Counter<long> _payments;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _payments = meter.CreateCounter<long>("payments", description: "Payment requests by outcome.");
    }

    public void Record(string outcome)
    {
        _payments.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}