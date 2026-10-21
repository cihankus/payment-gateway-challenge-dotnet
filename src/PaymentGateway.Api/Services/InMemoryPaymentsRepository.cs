using System.Collections.Concurrent;

using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public class InMemoryPaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException($"A payment with id {payment.Id} already exists.");

        return Task.CompletedTask;
    }

    public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _payments.TryGetValue(id, out var payment);

        return Task.FromResult(payment);
    }
}