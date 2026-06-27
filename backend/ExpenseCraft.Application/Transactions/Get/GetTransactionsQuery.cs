namespace ExpenseCraft.Application.Transactions.Get;

public sealed record GetTransactionsQuery(
    Guid UserId,
    int Limit);
