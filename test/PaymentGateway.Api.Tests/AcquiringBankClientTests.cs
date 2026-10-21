using System.Net;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Tests;

public class AcquiringBankClientTests
{
    [Fact]
    public async Task SendsThePaymentInTheFormatTheBankExpects()
    {
        // Arrange
        var handler = new StubBankHandler(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");
        var client = CreateClient(handler);

        // Act
        await client.AuthorizeAsync(Payment(), CancellationToken.None);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://bank.test/payments", handler.Request?.RequestUri?.ToString());

        using var body = JsonDocument.Parse(handler.RequestBody ?? "");
        Assert.Equal("2222405343248877", body.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("04/2027", body.RootElement.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal(100, body.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("123", body.RootElement.GetProperty("cvv").GetString());
    }

    [Fact]
    public async Task ReturnsAnAuthorisationWithItsCode()
    {
        // Arrange
        var handler = new StubBankHandler(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AuthorizeAsync(Payment(), CancellationToken.None);

        // Assert
        Assert.True(result.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", result.AuthorizationCode);
    }

    [Fact]
    public async Task ReturnsADecline()
    {
        // Arrange
        var handler = new StubBankHandler(HttpStatusCode.OK, """{"authorized":false,"authorization_code":""}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AuthorizeAsync(Payment(), CancellationToken.None);

        // Assert
        Assert.False(result.Authorized);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ThrowsWhenTheBankReturnsAnErrorStatus(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new StubBankHandler(statusCode, "{}");
        var client = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Payment(), CancellationToken.None));

        // Assert
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Theory]
    [InlineData("""{"authorization_code":"abc"}""")]   // no decision in the response
    [InlineData("not json")]
    [InlineData("null")]
    public async Task ThrowsWhenTheBankResponseCannotBeUnderstood(string responseBody)
    {
        // Arrange
        var handler = new StubBankHandler(HttpStatusCode.OK, responseBody);
        var client = CreateClient(handler);

        // Act and assert
        await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Payment(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsWhenTheBankCannotBeReached()
    {
        // Arrange
        var handler = new StubBankHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        // Act and assert
        await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Payment(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsWhenTheBankDoesNotRespondInTime()
    {
        // Arrange
        var handler = new StubBankHandler(neverResponds: true);
        var client = CreateClient(handler, TimeSpan.FromMilliseconds(100));

        // Act and assert
        await Assert.ThrowsAsync<AcquiringBankException>(
            () => client.AuthorizeAsync(Payment(), CancellationToken.None));
    }

    [Fact]
    public async Task LetsTheCallersCancellationThrough()
    {
        // Arrange
        var handler = new StubBankHandler(neverResponds: true);
        var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act and assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.AuthorizeAsync(Payment(), cancellation.Token));
    }

    private static ValidatedPaymentRequest Payment()
    {
        return new ValidatedPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }

    private static AcquiringBankClient CreateClient(StubBankHandler handler, TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://bank.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        };

        return new AcquiringBankClient(httpClient);
    }

    /// <summary>Stands in for the bank: records the request and returns a canned response.</summary>
    private class StubBankHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody = "";
        private readonly Exception? _exception;
        private readonly bool _neverResponds;

        public StubBankHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public StubBankHandler(Exception exception)
        {
            _exception = exception;
        }

        public StubBankHandler(bool neverResponds)
        {
            _neverResponds = neverResponds;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content != null)
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            if (_exception != null)
                throw _exception;

            if (_neverResponds)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}