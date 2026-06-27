namespace ExpenseCraft.Application.Chat;

public sealed record GetChatMessagesQuery(
    Guid UserId,
    int Limit);
