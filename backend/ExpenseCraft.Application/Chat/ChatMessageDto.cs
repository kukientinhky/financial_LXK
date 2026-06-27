using ExpenseCraft.Domain.Chat;

namespace ExpenseCraft.Application.Chat;

public sealed record ChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    string? Intent,
    Guid? TransactionId,
    DateTimeOffset CreatedAt);

internal static class ChatMessageMapping
{
    public static ChatMessageDto ToDto(this ChatMessage message)
    {
        return new ChatMessageDto(
            message.Id,
            ToApiValue(message.Role),
            message.Content,
            message.Intent.HasValue ? ToApiValue(message.Intent.Value) : null,
            message.TransactionId,
            message.CreatedAt);
    }

    public static string ToApiValue(ChatMessageRole role)
    {
        return role switch
        {
            ChatMessageRole.User => "user",
            ChatMessageRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Chat role is invalid.")
        };
    }

    public static string ToApiValue(ChatIntent intent)
    {
        return intent switch
        {
            ChatIntent.Income => "income",
            ChatIntent.Expense => "expense",
            ChatIntent.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Chat intent is invalid.")
        };
    }
}
