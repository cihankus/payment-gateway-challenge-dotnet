using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly IAcquiringBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IAcquiringBankClient bankClient, IPaymentsRepository paymentsRepository, ILogger<PaymentService> logger)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Sends the payment to the acquiring bank and stores the outcome.
    /// Throws <see cref="AcquiringBankException"/> when the bank gives no decision, in which case nothing is stored.
    /// </summary>
    public async Task<Payment> ProcessAsync(ValidatedPaymentRequest request, CancellationToken cancellationToken)
    {
        var authorization = await _bankClient.AuthorizeAsync(request, cancellationToken);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = authorization.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumberLastFour,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            AuthorizationCode = authorization.AuthorizationCode
        };

        // Once the bank has decided, the payment must be recorded even if the merchant has gone away,
        // otherwise an authorised payment would exist at the bank with no trace of it here.
        await _paymentsRepository.AddAsync(payment, CancellationToken.None);

        _logger.LogInformation(
            "Payment {PaymentId} {PaymentStatus} for {Amount} {Currency}",
            payment.Id, payment.Status, payment.Amount, payment.Currency);

        return payment;
    }
}