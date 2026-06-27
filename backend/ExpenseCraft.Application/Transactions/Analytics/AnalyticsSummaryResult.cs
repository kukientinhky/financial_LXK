namespace ExpenseCraft.Application.Transactions.Analytics;

public sealed record AnalyticsSummaryResult(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Net,
    string Currency,
    IReadOnlyList<CashflowByMonthResult> CashflowByMonth,
    IReadOnlyList<ExpenseByCategoryResult> ExpenseByCategory,
    ReasonablenessResult Reasonableness);

public sealed record CashflowByMonthResult(
    string Month,
    decimal Income,
    decimal Expense,
    decimal Net);

public sealed record ExpenseByCategoryResult(
    string Category,
    decimal Amount,
    decimal Percent);

public sealed record ReasonablenessResult(
    string Status,
    string Message,
    decimal? ExpenseRatio,
    IReadOnlyList<CategoryWarningResult> CategoryWarnings);

public sealed record CategoryWarningResult(
    string Category,
    decimal Amount,
    decimal Percent,
    string Message);
