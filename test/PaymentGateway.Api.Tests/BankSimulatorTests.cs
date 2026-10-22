using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

// Requires the bank simulator: docker compose up
// End to end: the real API with its real settings and bank client, talking to the simulator. Nothing is replaced.
public class BankSimulatorTests
{
    [Fact]
    public async Task AuthorizesACardEndingInAnOddDigit()
    {
        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/Payments", Request("2222405343248877"));
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Authorized, payment?.Status);
    }

    [Fact]
    public async Task DeclinesACardEndingInAnEvenDigit()
    {
        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/Payments", Request("2222405343248878"));
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, payment?.Status);
    }

    private static HttpClient CreateClient()
    {
        return new WebApplicationFactory<PaymentsController>().CreateClient();
    }

    private static PostPaymentRequest Request(string cardNumber)
    {
        return new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }
}