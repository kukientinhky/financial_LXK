namespace ExpenseCraft.Application.Chat;

public sealed record SaveChatMessageCommand(
    Guid UserId,
    string Role,
    string Content,
    string? Intent,
    Guid? TransactionId);
