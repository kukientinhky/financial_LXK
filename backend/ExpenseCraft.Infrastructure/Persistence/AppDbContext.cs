using ExpenseCraft.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ExpenseCraft.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users");

            builder.HasKey(user => user.Id);

            builder.Property(user => user.Id)
                .ValueGeneratedNever();

            builder.OwnsOne(user => user.Email, emailBuilder =>
            {
                emailBuilder.Property(email => email.Value)
                    .HasColumnName("email")
                    .HasMaxLength(255)
                    .IsRequired();

                emailBuilder.HasIndex(email => email.Value)
                    .IsUnique();
            });

            builder.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });
    }
}