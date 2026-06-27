using ExpenseCraft.Application.Transactions;
using ExpenseCraft.Domain.Transactions;

namespace ExpenseCraft.Application.Transactions.Analytics;

public sealed class GetAnalyticsSummaryHandler
{
    private const decimal HealthyExpenseRatio = 0.5m;
    private const decimal WatchExpenseRatio = 0.8m;
    private const decimal CategoryWarningPercent = 40m;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private readonly ITransactionRepository _transactionRepository;

    public GetAnalyticsSummaryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<AnalyticsSummaryResult> HandleAsync(
        GetAnalyticsSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(query));
        }

        if (query.To < query.From)
        {
            throw new ArgumentException("The 'to' date must be greater than or equal to the 'from' date.", nameof(query));
        }

        var transactions = await _transactionRepository.GetActiveInDateRangeAsync(
            query.UserId,
            ToVietnamUtcStart(query.From),
            ToVietnamUtcStart(query.To.AddDays(1)),
            cancellationToken);

        EnsureVndOnly(transactions);

        var totalIncome = transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);
        var totalExpense = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        var expenseByCategory = BuildExpenseByCategory(transactions, totalExpense);

        return new AnalyticsSummaryResult(
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            Transaction.DefaultCurrency,
            BuildCashflowByMonth(transactions),
            expenseByCategory,
            BuildReasonableness(totalIncome, totalExpense, expenseByCategory));
    }

    private static IReadOnlyList<CashflowByMonthResult> BuildCashflowByMonth(
        IReadOnlyList<Transaction> transactions)
    {
        return transactions
            .GroupBy(transaction =>
            {
                var vietnamOccurredAt = TimeZoneInfo.ConvertTime(
                    transaction.OccurredAt,
                    VietnamTimeZone);

                return new
                {
                    vietnamOccurredAt.Year,
                    vietnamOccurredAt.Month
                };
            })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group =>
            {
                var income = group
                    .Where(transaction => transaction.Type == TransactionType.Income)
                    .Sum(transaction => transaction.Amount);
                var expense = group
                    .Where(transaction => transaction.Type == TransactionType.Expense)
                    .Sum(transaction => transaction.Amount);

                return new CashflowByMonthResult(
                    $"{group.Key.Year:D4}-{group.Key.Month:D2}",
                    income,
                    expense,
                    income - expense);
            })
            .ToList();
    }

    private static IReadOnlyList<ExpenseByCategoryResult> BuildExpenseByCategory(
        IReadOnlyList<Transaction> transactions,
        decimal totalExpense)
    {
        return transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .GroupBy(transaction => transaction.Category)
            .Select(group =>
            {
                var amount = group.Sum(transaction => transaction.Amount);
                var percent = totalExpense == 0
                    ? 0
                    : Math.Round(amount / totalExpense * 100, 2);

                return new ExpenseByCategoryResult(group.Key, amount, percent);
            })
            .OrderByDescending(category => category.Amount)
            .ThenBy(category => category.Category)
            .ToList();
    }

    private static ReasonablenessResult BuildReasonableness(
        decimal totalIncome,
        decimal totalExpense,
        IReadOnlyList<ExpenseByCategoryResult> expenseByCategory)
    {
        var categoryWarnings = expenseByCategory
            .Where(category => category.Percent > CategoryWarningPercent)
            .Select(category => new CategoryWarningResult(
                category.Category,
                category.Amount,
                category.Percent,
                $"Category '{category.Category}' is over {CategoryWarningPercent}% of total expense."))
            .ToList();

        if (totalIncome <= 0)
        {
            return new ReasonablenessResult(
                "insufficient_data",
                "Income data is required to evaluate expense reasonableness.",
                null,
                categoryWarnings);
        }

        var expenseRatio = Math.Round(totalExpense / totalIncome, 4);

        if (expenseRatio <= HealthyExpenseRatio)
        {
            return new ReasonablenessResult(
                "healthy",
                "Expense level is healthy relative to income.",
                expenseRatio,
                categoryWarnings);
        }

        if (expenseRatio <= WatchExpenseRatio)
        {
            return new ReasonablenessResult(
                "watch",
                "Expense level should be watched relative to income.",
                expenseRatio,
                categoryWarnings);
        }

        return new ReasonablenessResult(
            "high",
            "Expense level is high relative to income.",
            expenseRatio,
            categoryWarnings);
    }

    private static void EnsureVndOnly(IReadOnlyList<Transaction> transactions)
    {
        var unsupportedTransaction = transactions.FirstOrDefault(transaction =>
            !string.Equals(
                transaction.Currency,
                Transaction.DefaultCurrency,
                StringComparison.OrdinalIgnoreCase));

        if (unsupportedTransaction is not null)
        {
            throw new ArgumentException(
                $"Analytics currently supports {Transaction.DefaultCurrency} transactions only. Unsupported currency '{unsupportedTransaction.Currency}' was found.",
                nameof(transactions));
        }
    }

    private static DateTimeOffset ToVietnamUtcStart(DateOnly date)
    {
        var vietnamLocalStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(vietnamLocalStart, VietnamTimeZone);

        return new DateTimeOffset(utcStart);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var timeZoneId in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Ho_Chi_Minh",
            TimeSpan.FromHours(7),
            "Asia/Ho_Chi_Minh",
            "Asia/Ho_Chi_Minh");
    }
}
