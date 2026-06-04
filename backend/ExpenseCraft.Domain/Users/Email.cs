using System.Text.RegularExpressions;

namespace ExpenseCraft.Domain.Users;

public sealed record Email
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    public Email(string value)
    {
        Value = value;
    }
    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email is required.", nameof(value));
        }

        var normalizedEmail = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalizedEmail))
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return new Email(normalizedEmail);
    }

    public override string ToString() => Value;
}