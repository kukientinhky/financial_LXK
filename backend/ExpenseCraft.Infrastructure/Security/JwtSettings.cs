namespace ExpenseCraft.Infrastructure.Security;
public sealed class JwtSettings
{    
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpirationInMinutes { get; init; }

}   