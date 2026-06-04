namespace ExpenseCraft.Domain.Users;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}