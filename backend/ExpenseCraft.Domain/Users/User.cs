namespace ExpenseCraft.Domain.Users;

public sealed class User
{
    private User()
    {
        Email = null!;
        PasswordHash = string.Empty;
    }

    private User(
        Guid id,
        Email email,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Register(Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password cannot be empty.", nameof(passwordHash));

        if (email == null)
            throw new ArgumentNullException(nameof(email));

        return new User(
            Guid.NewGuid(),
            email,
            passwordHash,
            DateTimeOffset.UtcNow);
    }
}