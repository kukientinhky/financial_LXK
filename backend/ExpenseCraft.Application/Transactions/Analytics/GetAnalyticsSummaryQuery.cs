namespace ExpenseCraft.Application.Transactions.Analytics;

public sealed record GetAnalyticsSummaryQuery(
    Guid UserId,
    DateOnly From,
    DateOnly To);
