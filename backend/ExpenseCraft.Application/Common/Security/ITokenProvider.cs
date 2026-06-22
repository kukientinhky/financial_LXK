using ExpenseCraft.Domain.Users;
namespace ExpenseCraft.Application.Common.Security;
public interface ITokenProvider
{
    string GenerateToken(User user);
}