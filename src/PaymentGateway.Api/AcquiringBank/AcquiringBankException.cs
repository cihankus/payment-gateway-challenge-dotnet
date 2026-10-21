using System.Net;

namespace PaymentGateway.Api.AcquiringBank;

/// <summary>
/// The acquiring bank could not give a decision: it was unreachable, timed out,
/// returned an error status or sent a response we could not understand.
/// </summary>
public class AcquiringBankException : Exception
{
    public AcquiringBankException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public AcquiringBankException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>The status code the bank responded with, when it responded at all.</summary>
    public HttpStatusCode? StatusCode { get; }
}