using ExpenseCraft.Domain.Chat;

namespace ExpenseCraft.Application.Chat;

public interface IChatMessageRepository
{
    Task AddAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
