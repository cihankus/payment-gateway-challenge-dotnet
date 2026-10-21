using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.AcquiringBank;

public interface IAcquiringBankClient
{
    /// <summary>
    /// Asks the acquiring bank to authorise the payment.
    /// Throws <see cref="AcquiringBankException"/> when no decision could be obtained from the bank.
    /// </summary>
    Task<BankAuthorizationResult> AuthorizeAsync(ValidatedPaymentRequest request, CancellationToken cancellationToken);
}