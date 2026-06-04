using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Domain.Users;

namespace ExpenseCraft.Application.Users.Register;
public sealed class RegisterHandler 
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email cannot be empty.", nameof(command.Email));

        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Password cannot be empty.", nameof(command.Password));
            
        if (command.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(command));
        }
        var email = Email.Create(command.Email);
        var emailAlreadyExists = await _userRepository.ExistsByEmailAsync(email, cancellationToken);
        if (emailAlreadyExists)
        {
            throw new InvalidOperationException("User with the specified email already exists.");
        }
        var passwordHash = _passwordHasher.Hash(command.Password);

        var user = User.Register(email, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id);
    }
}