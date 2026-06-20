using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Application.Users;
using ExpenseCraft.Domain.Users;

namespace ExpenseCraft.Application.Users.Login;

public sealed class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginUserResult> HandleAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(
            new Email(command.Email),
            cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }
        if (!_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }
        return new LoginUserResult(user.Id);
    }
}