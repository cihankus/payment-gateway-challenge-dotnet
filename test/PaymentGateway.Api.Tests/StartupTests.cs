using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Controllers;

namespace PaymentGateway.Api.Tests;

public class StartupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    public void RefusesToStartWithoutAValidBankUrl(string bankUrl)
    {
        // Arrange
        var factory = new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AcquiringBank:BaseUrl"] = bankUrl
                })));

        // Act and assert
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}