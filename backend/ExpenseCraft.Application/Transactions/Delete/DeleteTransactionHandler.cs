using ExpenseCraft.Application.Transactions;

namespace ExpenseCraft.Application.Transactions.Delete;

public sealed class DeleteTransactionHandler
{
    private readonly ITransactionRepository _transactionRepository;

    public DeleteTransactionHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<bool> HandleAsync(
        DeleteTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(command));
        }

        if (command.TransactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction id is required.", nameof(command));
        }

        var transaction = await _transactionRepository.GetActiveByIdForUserAsync(
            command.TransactionId,
            command.UserId,
            cancellationToken);

        if (transaction is null)
        {
            return false;
        }

        transaction.SoftDelete();
        await _transactionRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
