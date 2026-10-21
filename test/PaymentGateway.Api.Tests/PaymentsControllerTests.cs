using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private static readonly Payment StoredPayment = new()
    {
        Id = Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = "0123",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 1050
    };

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var client = await CreateClientWithPaymentAsync(StoredPayment);

        // Act
        var response = await client.GetAsync($"/api/Payments/{StoredPayment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PaymentResponse
        {
            Id = StoredPayment.Id,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "0123",
            ExpiryMonth = 4,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050
        }, paymentResponse);
    }

    [Fact]
    public async Task ReturnsStatusAsTextAndKeepsLeadingZerosInCardDigits()
    {
        // Arrange
        var client = await CreateClientWithPaymentAsync(StoredPayment);

        // Act
        var response = await client.GetAsync($"/api/Payments/{StoredPayment.Id}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal("Authorized", json.GetProperty("status").GetString());
        Assert.Equal("0123", json.GetProperty("cardNumberLastFour").GetString());
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<HttpClient> CreateClientWithPaymentAsync(Payment payment)
    {
        var paymentsRepository = new InMemoryPaymentsRepository();
        await paymentsRepository.AddAsync(payment);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        return webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IPaymentsRepository>(paymentsRepository)))
            .CreateClient();
    }
}