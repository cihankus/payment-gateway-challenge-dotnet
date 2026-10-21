using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

// Runs the whole API in memory, with the bank replaced by a fake and the clock fixed,
// so the tests do not depend on the simulator or on today's date.
public class PaymentsControllerTests
{
    private static readonly DateTimeOffset Today = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly Payment StoredPayment = new Payment
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
        var client = CreateClient(FakeAcquiringBankClient.Authorizing());

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreatesAnAuthorisedPayment()
    {
        // Arrange
        var client = CreateClient(FakeAcquiringBankClient.Authorizing());

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var payment = Assert.IsType<PaymentResponse>(await response.Content.ReadFromJsonAsync<PaymentResponse>());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/Payments/{payment.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2027, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(100, payment.Amount);
    }

    [Fact]
    public async Task CreatesADeclinedPayment()
    {
        // Arrange
        var client = CreateClient(FakeAcquiringBankClient.Declining());

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var payment = Assert.IsType<PaymentResponse>(await response.Content.ReadFromJsonAsync<PaymentResponse>());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public async Task MakesTheCreatedPaymentRetrievable()
    {
        // Arrange
        var client = CreateClient(FakeAcquiringBankClient.Authorizing());
        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act
        var getResponse = await client.GetAsync(postResponse.Headers.Location);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, retrieved);
    }

    // The one test that pins the exact messages, because this is the response merchants see.
    [Fact]
    public async Task RejectsAnInvalidPaymentWithoutCallingTheBank()
    {
        // Arrange
        var bank = FakeAcquiringBankClient.Authorizing();
        var client = CreateClient(bank);
        var request = ValidRequest();
        request.ExpiryMonth = 13;
        request.Cvv = null;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var problem = Assert.IsType<ValidationProblemDetails>(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Payment rejected", problem.Title);
        Assert.Equal(2, problem.Errors.Count);
        Assert.Equal(new[] { "Expiry month must be between 1 and 12." }, problem.Errors["expiryMonth"]);
        Assert.Equal(new[] { "CVV is required." }, problem.Errors["cvv"]);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Empty(bank.Requests);
    }

    // Bodies that cannot be read at all get ASP.NET's standard 400 response.
    [Theory]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":10.5,"cvv":"123"}""")]
    [InlineData("""{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":"abc","cvv":"123"}""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task RejectsUnreadableRequestsWithoutCallingTheBank(string body)
    {
        // Arrange
        var bank = FakeAcquiringBankClient.Authorizing();
        var client = CreateClient(bank);

        // Act
        var response = await client.PostAsync("/api/Payments", new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(bank.Requests);
    }

    [Fact]
    public async Task Returns502WhenTheBankGivesNoDecision()
    {
        // Arrange
        var bank = FakeAcquiringBankClient.Unavailable();
        var client = CreateClient(bank);

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var problem = Assert.IsType<ProblemDetails>(await response.Content.ReadFromJsonAsync<ProblemDetails>());

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Acquiring bank unavailable", problem.Title);
        Assert.Single(bank.Requests);
    }

    [Fact]
    public async Task NeverReturnsCardDataOrTheAuthorisationCode()
    {
        // Arrange
        var client = CreateClient(FakeAcquiringBankClient.Authorizing());

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("2222405343248877", responseBody);
        Assert.DoesNotContain("\"cvv\"", responseBody);
        Assert.DoesNotContain(FakeAcquiringBankClient.AuthorizationCode, responseBody);
    }

    private static PostPaymentRequest ValidRequest()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }

    private static async Task<HttpClient> CreateClientWithPaymentAsync(Payment payment)
    {
        var repository = new InMemoryPaymentsRepository();
        await repository.AddAsync(payment);

        return CreateClient(FakeAcquiringBankClient.Authorizing(), repository);
    }

    private static HttpClient CreateClient(IAcquiringBankClient bank, IPaymentsRepository? repository = null)
    {
        var factory = new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Today));
                services.AddSingleton(bank);
                if (repository != null)
                    services.AddSingleton(repository);
            }));

        return factory.CreateClient();
    }
}