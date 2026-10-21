using System.Net;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Tests;

/// <summary>Stands in for the acquiring bank: records what it was asked and answers with a fixed outcome.</summary>
public class FakeAcquiringBankClient : IAcquiringBankClient
{
    public const string AuthorizationCode = "0bb07405-6d44-4b50-a14f-7ae0beff13ad";

    private readonly bool _authorizes;
    private readonly bool _isUnavailable;

    private FakeAcquiringBankClient(bool authorizes, bool isUnavailable)
    {
        _authorizes = authorizes;
        _isUnavailable = isUnavailable;
    }

    public static FakeAcquiringBankClient Authorizing()
    {
        return new FakeAcquiringBankClient(true, false);
    }

    public static FakeAcquiringBankClient Declining()
    {
        return new FakeAcquiringBankClient(false, false);
    }

    public static FakeAcquiringBankClient Unavailable()
    {
        return new FakeAcquiringBankClient(false, true);
    }

    public List<ValidatedPaymentRequest> Requests { get; } = new List<ValidatedPaymentRequest>();

    public Task<BankAuthorizationResult> AuthorizeAsync(ValidatedPaymentRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_isUnavailable)
            throw new AcquiringBankException("The acquiring bank responded with status code 503.", HttpStatusCode.ServiceUnavailable);

        return Task.FromResult(new BankAuthorizationResult
        {
            Authorized = _authorizes,
            AuthorizationCode = _authorizes ? AuthorizationCode : ""
        });
    }
}