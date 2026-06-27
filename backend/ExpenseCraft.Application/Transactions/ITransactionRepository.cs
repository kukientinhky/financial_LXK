using ExpenseCraft.Domain.Transactions;

namespace ExpenseCraft.Application.Transactions;

public interface ITransactionRepository
{
    Task AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetActiveInDateRangeAsync(
        Guid userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);

    Task<Transaction?> GetActiveByIdForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveByIdForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
