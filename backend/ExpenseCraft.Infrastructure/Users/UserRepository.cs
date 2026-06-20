using ExpenseCraft.Application.Users;
using ExpenseCraft.Domain.Users;
using ExpenseCraft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseCraft.Infrastructure.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(
            user => user.Email.Value == email.Value,
            cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(
            user => user.Email.Value == email.Value,
            cancellationToken);
    }
    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
