using ExpenseCraft.Application.Transactions;
using ExpenseCraft.Domain.Transactions;
using ExpenseCraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseCraft.Infrastructure.Transactions;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _dbContext;

    public TransactionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Transactions.AddAsync(transaction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId && transaction.DeletedAt == null)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetActiveInDateRangeAsync(
        Guid userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.DeletedAt == null &&
                transaction.OccurredAt >= fromInclusive &&
                transaction.OccurredAt < toExclusive)
            .OrderBy(transaction => transaction.OccurredAt)
            .ThenBy(transaction => transaction.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Transaction?> GetActiveByIdForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Transactions
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.Id == id &&
                    transaction.UserId == userId &&
                    transaction.DeletedAt == null,
                cancellationToken);
    }

    public Task<bool> ExistsActiveByIdForUserAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Transactions
            .AnyAsync(
                transaction =>
                    transaction.Id == id &&
                    transaction.UserId == userId &&
                    transaction.DeletedAt == null,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
