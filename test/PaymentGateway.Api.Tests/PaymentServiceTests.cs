using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    // The API tests cover what the merchant sees. This one checks what is stored, which the API never shows in full.
    [Fact]
    public async Task StoresOnlyTheLastFourCardDigitsAndKeepsTheAuthorisationCode()
    {
        // Arrange
        var repository = new InMemoryPaymentsRepository();
        var service = new PaymentService(FakeAcquiringBankClient.Authorizing(), repository, NullLogger<PaymentService>.Instance);
        var request = new ValidatedPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var payment = await service.ProcessAsync(request, CancellationToken.None);
        var stored = await repository.GetAsync(payment.Id);

        // Assert
        Assert.Equal(new Payment
        {
            Id = payment.Id,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            AuthorizationCode = FakeAcquiringBankClient.AuthorizationCode
        }, stored);
    }
}