using ExpenseCraft.Application.Chat;
using ExpenseCraft.Domain.Chat;
using ExpenseCraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseCraft.Infrastructure.Chat;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly AppDbContext _dbContext;

    public ChatMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ChatMessages.AddAsync(message, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.UserId == userId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }
}
