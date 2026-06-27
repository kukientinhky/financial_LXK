namespace ExpenseCraft.Application.Transactions.Delete;

public sealed record DeleteTransactionCommand(
    Guid UserId,
    Guid TransactionId);
