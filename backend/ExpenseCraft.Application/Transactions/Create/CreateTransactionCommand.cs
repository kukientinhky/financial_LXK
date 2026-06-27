namespace ExpenseCraft.Application.Transactions.Create;

public sealed record CreateTransactionCommand(
    Guid UserId,
    string Type,
    decimal Amount,
    string? Currency,
    string Category,
    string? Note,
    string? Source,
    DateTimeOffset? OccurredAt);
