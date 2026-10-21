using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentRequestValidator _validator;
    private readonly PaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentsRepository paymentsRepository,
        PaymentRequestValidator validator,
        PaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponse>> PostPaymentAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var validated = _validator.Validate(request, out var errors);
        if (validated == null)
        {
            // Only the field names are logged, never the submitted values.
            _logger.LogInformation("Payment rejected. Invalid fields: {InvalidFields}", string.Join(", ", errors.Keys));
            return PaymentRejected(errors);
        }

        Payment payment;
        try
        {
            payment = await _paymentService.ProcessAsync(validated, cancellationToken);
        }
        catch (AcquiringBankException exception)
        {
            _logger.LogWarning(exception, "Acquiring bank gave no decision. Bank status code: {BankStatusCode}", (int?)exception.StatusCode);
            return Problem(
                title: "Acquiring bank unavailable",
                detail: "The acquiring bank did not return a decision, so no payment was created.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return CreatedAtRoute("GetPayment", new { id = payment.Id }, PaymentResponse.FromPayment(payment));
    }

    private ActionResult PaymentRejected(Dictionary<string, string[]> errors)
    {
        foreach (var error in errors)
        {
            foreach (var message in error.Value)
                ModelState.AddModelError(error.Key, message);
        }

        return ValidationProblem(
            title: "Payment rejected",
            detail: "The payment was not sent to the acquiring bank because the request is invalid.",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpGet("{id:guid}", Name = "GetPayment")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _paymentsRepository.GetAsync(id, cancellationToken);

        if (payment == null)
            return NotFound();

        return PaymentResponse.FromPayment(payment);
    }
}