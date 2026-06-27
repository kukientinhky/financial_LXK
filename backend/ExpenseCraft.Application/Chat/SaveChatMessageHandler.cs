using ExpenseCraft.Application.Transactions;
using ExpenseCraft.Domain.Chat;

namespace ExpenseCraft.Application.Chat;

public sealed class SaveChatMessageHandler
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ITransactionRepository _transactionRepository;

    public SaveChatMessageHandler(
        IChatMessageRepository chatMessageRepository,
        ITransactionRepository transactionRepository)
    {
        _chatMessageRepository = chatMessageRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<ChatMessageDto> HandleAsync(
        SaveChatMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TransactionId.HasValue)
        {
            var activeTransactionBelongsToUser = await _transactionRepository.ExistsActiveByIdForUserAsync(
                command.TransactionId.Value,
                command.UserId,
                cancellationToken);

            if (!activeTransactionBelongsToUser)
            {
                throw new KeyNotFoundException("Transaction was not found for the current user.");
            }
        }

        var message = ChatMessage.Create(
            command.UserId,
            ParseRole(command.Role),
            command.Content,
            ParseIntent(command.Intent),
            command.TransactionId);

        await _chatMessageRepository.AddAsync(message, cancellationToken);

        return message.ToDto();
    }

    private static ChatMessageRole ParseRole(string role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "user" => ChatMessageRole.User,
            "assistant" => ChatMessageRole.Assistant,
            _ => throw new ArgumentException("Chat role must be 'user' or 'assistant'.", nameof(role))
        };
    }

    private static ChatIntent? ParseIntent(string? intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return null;
        }

        return intent.Trim().ToLowerInvariant() switch
        {
            "income" => ChatIntent.Income,
            "expense" => ChatIntent.Expense,
            "unknown" => ChatIntent.Unknown,
            _ => throw new ArgumentException("Chat intent must be 'income', 'expense', or 'unknown'.", nameof(intent))
        };
    }
}
