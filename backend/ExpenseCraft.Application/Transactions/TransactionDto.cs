using ExpenseCraft.Domain.Transactions;

namespace ExpenseCraft.Application.Transactions;

public sealed record TransactionDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Currency,
    string Category,
    string? Note,
    string? Source,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt);

internal static class TransactionMapping
{
    public static TransactionDto ToDto(this Transaction transaction)
    {
        return new TransactionDto(
            transaction.Id,
            ToApiValue(transaction.Type),
            transaction.Amount,
            transaction.Currency,
            transaction.Category,
            transaction.Note,
            transaction.Source,
            transaction.OccurredAt,
            transaction.CreatedAt);
    }

    public static string ToApiValue(TransactionType type)
    {
        return type switch
        {
            TransactionType.Income => "income",
            TransactionType.Expense => "expense",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Transaction type is invalid.")
        };
    }
}
