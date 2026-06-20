using ExpenseCraft.Domain.Users;

namespace ExpenseCraft.Application.Users;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}
