using ExpenseCraft.Domain.Chat;
using ExpenseCraft.Domain.Transactions;
using ExpenseCraft.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExpenseCraft.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var transactionTypeConverter = new ValueConverter<TransactionType, string>(
            type => ToTransactionTypeValue(type),
            value => ParseTransactionType(value));
        var chatRoleConverter = new ValueConverter<ChatMessageRole, string>(
            role => ToChatRoleValue(role),
            value => ParseChatRole(value));
        var chatIntentConverter = new ValueConverter<ChatIntent?, string?>(
            intent => intent.HasValue ? ToChatIntentValue(intent.Value) : null,
            value => value == null ? null : ParseChatIntent(value));

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

        modelBuilder.Entity<Transaction>(builder =>
        {
            builder.ToTable("transactions");

            builder.HasKey(transaction => transaction.Id);

            builder.Property(transaction => transaction.Id)
                .ValueGeneratedNever();

            builder.Property(transaction => transaction.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(transaction => transaction.Type)
                .HasConversion(transactionTypeConverter)
                .HasColumnName("type")
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(transaction => transaction.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(transaction => transaction.Currency)
                .HasColumnName("currency")
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(transaction => transaction.Category)
                .HasColumnName("category")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(transaction => transaction.Note)
                .HasColumnName("note")
                .HasMaxLength(500);

            builder.Property(transaction => transaction.Source)
                .HasColumnName("source")
                .HasMaxLength(100);

            builder.Property(transaction => transaction.OccurredAt)
                .HasColumnName("occurred_at")
                .IsRequired();

            builder.Property(transaction => transaction.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(transaction => transaction.DeletedAt)
                .HasColumnName("deleted_at");

            builder.HasIndex(transaction => new
            {
                transaction.UserId,
                transaction.OccurredAt
            });

            builder.HasIndex(transaction => transaction.DeletedAt);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(transaction => transaction.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(builder =>
        {
            builder.ToTable("chat_messages");

            builder.HasKey(message => message.Id);

            builder.Property(message => message.Id)
                .ValueGeneratedNever();

            builder.Property(message => message.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(message => message.Role)
                .HasConversion(chatRoleConverter)
                .HasColumnName("role")
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(message => message.Content)
                .HasColumnName("content")
                .HasMaxLength(4000)
                .IsRequired();

            builder.Property(message => message.Intent)
                .HasConversion(chatIntentConverter)
                .HasColumnName("intent")
                .HasMaxLength(20);

            builder.Property(message => message.TransactionId)
                .HasColumnName("transaction_id");

            builder.Property(message => message.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.HasIndex(message => new
            {
                message.UserId,
                message.CreatedAt
            });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(message => message.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Transaction>()
                .WithMany()
                .HasForeignKey(message => message.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static string ToTransactionTypeValue(TransactionType type)
    {
        return type switch
        {
            TransactionType.Income => "income",
            TransactionType.Expense => "expense",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Transaction type is invalid.")
        };
    }

    private static TransactionType ParseTransactionType(string value)
    {
        return value switch
        {
            "income" => TransactionType.Income,
            "expense" => TransactionType.Expense,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Transaction type is invalid.")
        };
    }

    private static string ToChatRoleValue(ChatMessageRole role)
    {
        return role switch
        {
            ChatMessageRole.User => "user",
            ChatMessageRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Chat role is invalid.")
        };
    }

    private static ChatMessageRole ParseChatRole(string value)
    {
        return value switch
        {
            "user" => ChatMessageRole.User,
            "assistant" => ChatMessageRole.Assistant,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Chat role is invalid.")
        };
    }

    private static string ToChatIntentValue(ChatIntent intent)
    {
        return intent switch
        {
            ChatIntent.Income => "income",
            ChatIntent.Expense => "expense",
            ChatIntent.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Chat intent is invalid.")
        };
    }

    private static ChatIntent ParseChatIntent(string value)
    {
        return value switch
        {
            "income" or "thu" => ChatIntent.Income,
            "expense" or "chi" => ChatIntent.Expense,
            "unknown" => ChatIntent.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Chat intent is invalid.")
        };
    }
}
