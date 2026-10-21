using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.AcquiringBank;

/// <summary>
/// Talks to the acquiring bank over HTTP. Requests are never retried automatically: a payment
/// authorisation is not idempotent, so sending it twice could charge the shopper twice.
/// </summary>
public class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;

    public AcquiringBankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankAuthorizationResult> AuthorizeAsync(ValidatedPaymentRequest request, CancellationToken cancellationToken)
    {
        var bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = request.ExpiryMonth.ToString("D2") + "/" + request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        using var response = await PostAsync(bankRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new AcquiringBankException($"The acquiring bank responded with status code {(int)response.StatusCode}.", response.StatusCode);

        var bankResponse = await ReadResponseAsync(response, cancellationToken);

        if (bankResponse == null || bankResponse.Authorized == null)
            throw new AcquiringBankException("The acquiring bank response did not say whether the payment was authorised.");

        return new BankAuthorizationResult
        {
            Authorized = bankResponse.Authorized.Value,
            AuthorizationCode = bankResponse.AuthorizationCode
        };
    }

    private async Task<HttpResponseMessage> PostAsync(BankPaymentRequest bankRequest, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.PostAsJsonAsync("payments", bankRequest, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AcquiringBankException("The acquiring bank could not be reached.", exception);
        }
        catch (TaskCanceledException exception)
        {
            // Cancelled by our caller, for example because the merchant disconnected: not a bank failure.
            if (cancellationToken.IsCancellationRequested)
                throw;

            throw new AcquiringBankException("The acquiring bank did not respond in time.", exception);
        }
    }

    private static async Task<BankPaymentResponse?> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new AcquiringBankException("The acquiring bank response could not be read.", exception);
        }
    }
}