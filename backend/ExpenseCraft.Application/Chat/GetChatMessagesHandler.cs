namespace ExpenseCraft.Application.Chat;

public sealed class GetChatMessagesHandler
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetChatMessagesHandler(IChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> HandleAsync(
        GetChatMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(query));
        }

        var limit = Math.Clamp(query.Limit, 1, 200);
        var messages = await _chatMessageRepository.GetRecentAsync(
            query.UserId,
            limit,
            cancellationToken);

        return messages
            .Select(message => message.ToDto())
            .ToList();
    }
}
