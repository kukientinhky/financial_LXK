namespace ExpenseCraft.Domain.Transactions;

public sealed class Transaction
{
    public const string DefaultCurrency = "VND";

    private Transaction()
    {
        Currency = DefaultCurrency;
        Category = string.Empty;
    }

    private Transaction(
        Guid id,
        Guid userId,
        TransactionType type,
        decimal amount,
        string currency,
        string category,
        string? note,
        string? source,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Amount = amount;
        Currency = currency;
        Category = category;
        Note = note;
        Source = source;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string Category { get; private set; }
    public string? Note { get; private set; }
    public string? Source { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Transaction Create(
        Guid userId,
        TransactionType type,
        decimal amount,
        string? currency,
        string category,
        string? note,
        string? source,
        DateTimeOffset? occurredAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Transaction type is invalid.", nameof(type));
        }

        var normalizedAmount = NormalizeAmount(amount);

        if (normalizedAmount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.", nameof(category));
        }

        var now = DateTimeOffset.UtcNow;
        var normalizedOccurredAt = occurredAt?.ToUniversalTime() ?? now;

        return new Transaction(
            Guid.NewGuid(),
            userId,
            type,
            normalizedAmount,
            NormalizeCurrency(currency),
            NormalizeRequired(category, 100, nameof(category)),
            NormalizeOptional(note, 500, nameof(note)),
            NormalizeOptional(source, 100, nameof(source)),
            normalizedOccurredAt,
            now);
    }

    public void SoftDelete(DateTimeOffset? deletedAt = null)
    {
        DeletedAt ??= deletedAt ?? DateTimeOffset.UtcNow;
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return DefaultCurrency;
        }

        var normalizedCurrency = NormalizeRequired(currency, 10, nameof(currency)).ToUpperInvariant();
        if (normalizedCurrency != DefaultCurrency)
        {
            throw new ArgumentException("Only VND currency is supported.", nameof(currency));
        }

        return normalizedCurrency;
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequired(value, maxLength, parameterName);
    }
}
