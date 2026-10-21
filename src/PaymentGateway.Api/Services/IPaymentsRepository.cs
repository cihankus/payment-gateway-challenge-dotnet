using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}