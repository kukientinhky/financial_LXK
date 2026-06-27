namespace ExpenseCraft.Domain.Chat;

public sealed class ChatMessage
{
    private ChatMessage()
    {
        Content = string.Empty;
    }

    private ChatMessage(
        Guid id,
        Guid userId,
        ChatMessageRole role,
        string content,
        ChatIntent? intent,
        Guid? transactionId,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Role = role;
        Content = content;
        Intent = intent;
        TransactionId = transactionId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public ChatMessageRole Role { get; private set; }
    public string Content { get; private set; }
    public ChatIntent? Intent { get; private set; }
    public Guid? TransactionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ChatMessage Create(
        Guid userId,
        ChatMessageRole role,
        string content,
        ChatIntent? intent,
        Guid? transactionId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("Chat role is invalid.", nameof(role));
        }

        if (intent.HasValue && !Enum.IsDefined(intent.Value))
        {
            throw new ArgumentException("Chat intent is invalid.", nameof(intent));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        var normalizedContent = content.Trim();
        if (normalizedContent.Length > 4000)
        {
            throw new ArgumentException("Message content cannot exceed 4000 characters.", nameof(content));
        }

        return new ChatMessage(
            Guid.NewGuid(),
            userId,
            role,
            normalizedContent,
            intent,
            transactionId,
            DateTimeOffset.UtcNow);
    }
}
