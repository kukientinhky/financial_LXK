using ExpenseCraft.Application.Transactions;

namespace ExpenseCraft.Application.Transactions.Get;

public sealed class GetTransactionsHandler
{
    private readonly ITransactionRepository _transactionRepository;

    public GetTransactionsHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<IReadOnlyList<TransactionDto>> HandleAsync(
        GetTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(query));
        }

        var limit = Math.Clamp(query.Limit, 1, 100);
        var transactions = await _transactionRepository.GetRecentAsync(
            query.UserId,
            limit,
            cancellationToken);

        return transactions
            .Select(transaction => transaction.ToDto())
            .ToList();
    }
}
