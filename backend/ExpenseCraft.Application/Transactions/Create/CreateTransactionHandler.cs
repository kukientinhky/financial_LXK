using ExpenseCraft.Application.Transactions;
using ExpenseCraft.Domain.Transactions;

namespace ExpenseCraft.Application.Transactions.Create;

public sealed class CreateTransactionHandler
{
    private readonly ITransactionRepository _transactionRepository;

    public CreateTransactionHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<TransactionDto> HandleAsync(
        CreateTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var transaction = Transaction.Create(
            command.UserId,
            ParseTransactionType(command.Type),
            command.Amount,
            command.Currency,
            command.Category,
            command.Note,
            command.Source,
            command.OccurredAt);

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        return transaction.ToDto();
    }

    private static TransactionType ParseTransactionType(string type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "income" => TransactionType.Income,
            "expense" => TransactionType.Expense,
            _ => throw new ArgumentException("Transaction type must be 'income' or 'expense'.", nameof(type))
        };
    }
}
