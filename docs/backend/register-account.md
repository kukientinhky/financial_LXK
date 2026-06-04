# Register Account Backend Guide

Tài liệu này giải thích feature đăng ký tài khoản trong backend `ExpenseCraft` theo hướng Clean Architecture, SOLID và DDD. Nội dung viết cho người mới học backend, nên mỗi phần đều có mục tiêu, code chính và lý do tại sao code như vậy.

## 1. Tổng Quan

Feature register account dùng để tạo tài khoản người dùng mới.

Endpoint:

```http
POST /api/users/register
```

Input:

```json
{
  "email": "test@example.com",
  "password": "Password123!"
}
```

Output thành công:

```http
201 Created
```

```json
{
  "userId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Luồng xử lý tổng quát:

```text
HTTP Request
-> API endpoint
-> RegisterHandler trong Application
-> User và Email trong Domain
-> UserRepository trong Infrastructure
-> PostgreSQL database
```

Ý tưởng quan trọng:

```text
API chỉ nhận request và trả response.
Application xử lý use case.
Domain giữ business rule.
Infrastructure xử lý database, EF Core, password hashing.
```

## 2. Kiến Trúc Project

Backend đang chia thành 4 project chính:

```text
backend/
  ExpenseCraft.Api
  ExpenseCraft.Application
  ExpenseCraft.Domain
  ExpenseCraft.Infrastructure
```

Vai trò từng project:

```text
ExpenseCraft.Domain
Chứa business model và business rule. Không phụ thuộc database, HTTP, EF Core.

ExpenseCraft.Application
Chứa use case của app. Ví dụ: đăng ký user, login, tạo expense.

ExpenseCraft.Infrastructure
Chứa kỹ thuật cụ thể. Ví dụ: EF Core, PostgreSQL, BCrypt.

ExpenseCraft.Api
Chứa HTTP endpoint, Swagger, dependency injection.
```

Dependency đúng:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application + Domain
```

Dependency không nên có:

```text
Domain -> Infrastructure
Domain -> Api
Application -> Api
Application -> Infrastructure
```

Lý do clean:

```text
Business logic không bị dính với database hoặc HTTP.
Sau này đổi PostgreSQL sang SQL Server thì Domain và Application ít bị ảnh hưởng.
Code dễ test hơn vì Application phụ thuộc interface thay vì class cụ thể.
```

## 3. Domain Layer

Domain layer hiện có:

```text
ExpenseCraft.Domain/
  Users/
    Email.cs
    User.cs
    IUserRepository.cs
```

### 3.1. Email Value Object

File:

```text
backend/ExpenseCraft.Domain/Users/Email.cs
```

Code chính:

```csharp
using System.Text.RegularExpressions;

namespace ExpenseCraft.Domain.Users;

public sealed record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
```

`Email` là Value Object.

Value Object nghĩa là object được so sánh bằng giá trị, không phải bằng identity riêng.

Ví dụ:

```csharp
var email1 = Email.Create("Test@Example.com");
var email2 = Email.Create("test@example.com");
```

Sau khi normalize, cả hai đều là:

```text
test@example.com
```

Lý do clean:

```text
Email không chỉ là string.
Email tự kiểm tra format.
Email tự normalize thành lowercase.
Code khác không cần lặp lại logic validate email.
```

Điểm nên cải thiện:

```csharp
public Email(string value)
```

Nên đổi thành:

```csharp
private Email(string value)
```

Lý do:

```text
Nếu constructor public, người khác có thể viết new Email("abc") và bypass validation.
Nếu constructor private, mọi người bắt buộc dùng Email.Create(...).
```

### 3.2. User Entity

File:

```text
backend/ExpenseCraft.Domain/Users/User.cs
```

Code chính:

```csharp
namespace ExpenseCraft.Domain.Users;

public sealed class User
{
    private User()
    {
        Email = null!;
        PasswordHash = string.Empty;
    }

    private User(
        Guid id,
        Email email,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Register(Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(passwordHash));
        }

        if (email == null)
        {
            throw new ArgumentNullException(nameof(email));
        }

        return new User(
            Guid.NewGuid(),
            email,
            passwordHash,
            DateTimeOffset.UtcNow);
    }
}
```

`User` là Entity.

Entity nghĩa là object có identity riêng. Với `User`, identity là:

```csharp
public Guid Id { get; private set; }
```

Lý do dùng `User.Register(...)`:

```csharp
var user = User.Register(email, passwordHash);
```

Cách này tốt hơn:

```csharp
var user = new User();
user.Email = email;
user.PasswordHash = passwordHash;
```

Vì `User.Register(...)` thể hiện business action rõ ràng:

```text
Đăng ký một user mới.
```

Lý do clean:

```text
Constructor chính là private nên không ai tạo User sai cách.
Property có private set nên code bên ngoài không sửa lung tung.
Factory method Register đảm bảo User tạo ra luôn có Id, Email, PasswordHash, CreatedAt.
```

Vì sao có constructor rỗng private:

```csharp
private User()
{
    Email = null!;
    PasswordHash = string.Empty;
}
```

Lý do:

```text
EF Core cần constructor rỗng để tạo object khi đọc data từ database.
Constructor này private nên code bên ngoài không gọi được.
Đây là compromise phổ biến khi dùng EF Core với DDD entity.
```

### 3.3. IUserRepository

File:

```text
backend/ExpenseCraft.Domain/Users/IUserRepository.cs
```

Code:

```csharp
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
```

Lý do cần interface:

```text
Application cần lưu user, nhưng không nên biết lưu bằng PostgreSQL hay gì khác.
IUserRepository là contract.
Infrastructure sẽ implement contract này bằng EF Core.
```

Đây là Dependency Inversion trong SOLID:

```text
Code cấp cao phụ thuộc abstraction, không phụ thuộc implementation cụ thể.
```

## 4. Application Layer

Application layer hiện có:

```text
ExpenseCraft.Application/
  Common/
    Security/
      IPasswordHasher.cs
  Users/
    Register/
      RegisterUserCommand.cs
      RegisterUserResult.cs
      RegisterHandler.cs
```

### 4.1. RegisterUserCommand

File:

```text
backend/ExpenseCraft.Application/Users/Register/RegisterUserCommand.cs
```

Code:

```csharp
namespace ExpenseCraft.Application.Users.Register;

public sealed record RegisterUserCommand(
    string Email,
    string Password);
```

Command là input của use case.

Nó không phải HTTP request. Nó chỉ mô tả mong muốn của app:

```text
Tôi muốn đăng ký user mới với Email và Password.
```

Lý do clean:

```text
API request và Application command tách nhau.
Sau này use case này có thể được gọi từ nơi khác, không chỉ HTTP.
Ví dụ: CLI, message queue, background job.
```

Vì sao không dùng interface cho Command:

```text
Command chỉ chứa data.
Interface nên dùng cho behavior có nhiều implementation.
```

### 4.2. RegisterUserResult

File:

```text
backend/ExpenseCraft.Application/Users/Register/RegisterUserResult.cs
```

Code:

```csharp
namespace ExpenseCraft.Application.Users.Register;

public sealed record RegisterUserResult(Guid UserId);
```

Result là output của use case.

Lý do clean:

```text
Application trả về kết quả nghiệp vụ.
Application không trả HTTP 201 hay HTTP 400.
HTTP status là trách nhiệm của API layer.
```

### 4.3. IPasswordHasher

File:

```text
backend/ExpenseCraft.Application/Common/Security/IPasswordHasher.cs
```

Code:

```csharp
namespace ExpenseCraft.Application.Common.Security;

public interface IPasswordHasher
{
    string Hash(string password);
}
```

Lý do cần interface:

```text
RegisterHandler cần hash password.
Nhưng RegisterHandler không nên biết BCrypt là gì.
Ngày mai đổi BCrypt sang Argon2 thì Application không cần sửa nhiều.
```

### 4.4. RegisterHandler

File:

```text
backend/ExpenseCraft.Application/Users/Register/RegisterHandler.cs
```

Code chính:

```csharp
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

    public async Task<RegisterUserResult> HandleAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(command.Email));
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(command.Password));
        }

        if (command.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(command));
        }

        var email = Email.Create(command.Email);

        var emailAlreadyExists = await _userRepository.ExistsByEmailAsync(
            email,
            cancellationToken);

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
```

RegisterHandler là use case orchestration.

Nó làm theo thứ tự:

```text
Validate password input.
Tạo Email value object.
Kiểm tra email đã tồn tại chưa.
Hash password.
Tạo User domain entity.
Lưu User.
Trả UserId.
```

Lý do clean:

```text
Handler không biết HTTP.
Handler không biết PostgreSQL.
Handler không biết EF Core.
Handler chỉ biết interface IUserRepository và IPasswordHasher.
```

Điểm nên cải thiện:

```text
Tên RegisterHandler nên đổi thành RegisterUserHandler để rõ nghĩa hơn.
Hiện tại throw exception cho business error, sau này nên dùng Result pattern.
```

## 5. Infrastructure Layer

Infrastructure layer hiện có:

```text
ExpenseCraft.Infrastructure/
  Persistence/
    AppDbContext.cs
  Security/
    PasswordHasher.cs
  Users/
    UserRepository.cs
```

### 5.1. AppDbContext

File:

```text
backend/ExpenseCraft.Infrastructure/Persistence/AppDbContext.cs
```

Code chính:

```csharp
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
```

Lý do clean:

```text
Mapping database nằm ở Infrastructure.
Domain không cần biết table users là gì.
Email là owned type, nên EF lưu Email.Value thành column email.
```

Điểm nên cải thiện:

```csharp
builder.Property(user => user.Id)
    .HasColumnName("id")
    .ValueGeneratedNever();
```

Lý do:

```text
PostgreSQL thường dùng lowercase column name.
Hiện migration tạo column "Id" viết hoa vì chưa map HasColumnName("id").
```

### 5.2. UserRepository

File:

```text
backend/ExpenseCraft.Infrastructure/Users/UserRepository.cs
```

Code:

```csharp
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

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

Lý do clean:

```text
Repository là nơi nói chuyện với database.
Application chỉ gọi IUserRepository.
EF Core bị giữ trong Infrastructure, không rò rỉ vào Application.
```

### 5.3. PasswordHasher

File:

```text
backend/ExpenseCraft.Infrastructure/Security/PasswordHasher.cs
```

Code:

```csharp
using ExpenseCraft.Application.Common.Security;

namespace ExpenseCraft.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
```

Lý do clean:

```text
BCrypt là chi tiết kỹ thuật.
Chi tiết kỹ thuật đặt ở Infrastructure.
Application chỉ biết IPasswordHasher.
```

Lưu ý bảo mật:

```text
Không bao giờ lưu plain password vào database.
Luôn lưu password hash.
BCrypt tự tạo salt nên an toàn hơn hash thường như SHA256.
```

## 6. API Layer

File:

```text
backend/ExpenseCraft.Api/Program.cs
```

Code chính:

```csharp
using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Application.Users.Register;
using ExpenseCraft.Domain.Users;
using ExpenseCraft.Infrastructure.Persistence;
using ExpenseCraft.Infrastructure.Security;
using ExpenseCraft.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<RegisterHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/users/register", async (
    RegisterUserRequest request,
    RegisterHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new RegisterUserCommand(request.Email, request.Password),
        cancellationToken);

    return Results.Created($"/api/users/{result.UserId}", result);
})
.WithName("RegisterUser")
.WithOpenApi();

app.Run();

public sealed record RegisterUserRequest(
    string Email,
    string Password);
```

Lý do API code như vậy:

```text
API chỉ nhận request.
API convert request thành command.
API gọi handler.
API trả HTTP response.
```

API không làm những việc này:

```text
Không validate email format chi tiết.
Không hash password.
Không gọi DbContext trực tiếp.
Không tự tạo User entity.
```

Lý do clean:

```text
API mỏng, dễ đọc.
Business logic nằm trong Application và Domain.
Nếu sau này đổi từ Minimal API sang Controller, use case không bị ảnh hưởng.
```

## 7. Database Và Migration

Connection string trong `ExpenseCraft.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Database": "Host=localhost;Port=5432;Database=mydatabase;Username=admin;Password=123456"
  }
}
```

Docker PostgreSQL trong `docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: 123456
      POSTGRES_DB: mydatabase
    ports:
      - "5432:5432"
```

Packages đã dùng:

```text
Microsoft.EntityFrameworkCore 8.0.27
Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11
BCrypt.Net-Next 4.2.0
Microsoft.EntityFrameworkCore.Design 8.0.27
```

Tạo migration:

```bash
dotnet ef migrations add CreateUsersTable \
  --project ExpenseCraft.Infrastructure \
  --startup-project ExpenseCraft.Api
```

Apply migration:

```bash
dotnet ef database update \
  --project ExpenseCraft.Infrastructure \
  --startup-project ExpenseCraft.Api
```

Table được tạo:

```sql
CREATE TABLE users (
    "Id" uuid NOT NULL,
    email character varying(255) NOT NULL,
    password_hash character varying(500) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_users" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_users_email" ON users (email);
```

## 8. Test API

Chạy PostgreSQL:

```bash
docker compose up -d
```

Chạy backend:

```bash
dotnet run --project ExpenseCraft.Api
```

Mở Swagger:

```text
http://localhost:{PORT}/swagger
```

Gửi request:

```json
{
  "email": "test@example.com",
  "password": "Password123!"
}
```

Test bằng curl:

```bash
curl -X POST http://localhost:{PORT}/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Password123!"}'
```

Kết quả mong đợi:

```json
{
  "userId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

## 9. SOLID Trong Feature Này

Single Responsibility Principle:

```text
Email chỉ validate và lưu email.
User chỉ quản lý trạng thái user.
RegisterHandler chỉ xử lý use case register.
UserRepository chỉ xử lý database cho user.
PasswordHasher chỉ hash password.
```

Open Closed Principle:

```text
Muốn đổi BCrypt sang Argon2, tạo implementation mới của IPasswordHasher.
RegisterHandler không cần đổi nhiều.
```

Interface Segregation Principle:

```text
IUserRepository nhỏ, chỉ có method app cần.
IPasswordHasher nhỏ, chỉ có method Hash.
Không tạo interface lớn kiểu IDataService chứa mọi thứ.
```

Dependency Inversion Principle:

```text
RegisterHandler phụ thuộc IUserRepository và IPasswordHasher.
Nó không phụ thuộc UserRepository hoặc BCrypt trực tiếp.
```

## 10. Những Lỗi Đã Gặp

### 10.1. EF Core version không hợp net8

Lỗi:

```text
Package Microsoft.EntityFrameworkCore 10.x is not compatible with net8.0
```

Nguyên nhân:

```text
Project dùng net8.0 nhưng package mặc định lấy version mới nhất cho net10.0.
```

Cách sửa:

```bash
dotnet add ExpenseCraft.Infrastructure package Microsoft.EntityFrameworkCore --version 8.0.27
dotnet add ExpenseCraft.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
```

### 10.2. EF không tạo được User

Lỗi:

```text
No suitable constructor was found for entity type 'User'
```

Nguyên nhân:

```text
User chỉ có constructor nhận Email owned type.
EF Core không bind owned type vào constructor đó được.
```

Cách sửa:

```csharp
private User()
{
    Email = null!;
    PasswordHash = string.Empty;
}
```

### 10.3. Table users already exists

Lỗi:

```text
42P07: relation "users" already exists
```

Nguyên nhân:

```text
Bạn đã tạo table users bằng SQL trước đó.
EF migration cũng cố tạo lại table users.
```

Cách xử lý nếu table chưa có data quan trọng:

```sql
DROP TABLE users;
```

Sau đó chạy lại:

```bash
dotnet ef database update \
  --project ExpenseCraft.Infrastructure \
  --startup-project ExpenseCraft.Api
```

## 11. Những Điểm Nên Cải Thiện Tiếp Theo

### 11.1. Error Handling

Hiện tại handler đang dùng exception:

```csharp
throw new InvalidOperationException("User with the specified email already exists.");
```

Vấn đề:

```text
API có thể trả 500 Internal Server Error cho lỗi business như duplicate email.
Duplicate email nên là 409 Conflict hoặc 400 Bad Request.
```

Hướng cải thiện:

```text
Dùng Result pattern.
Hoặc bắt exception ở API và map sang HTTP status phù hợp.
```

### 11.2. Đổi tên RegisterHandler

Hiện tại:

```csharp
public sealed class RegisterHandler
```

Nên đổi thành:

```csharp
public sealed class RegisterUserHandler
```

Lý do:

```text
Tên rõ hơn.
Khớp với RegisterUserCommand và RegisterUserResult.
```

### 11.3. Đổi Email constructor thành private

Hiện tại:

```csharp
public Email(string value)
```

Nên đổi thành:

```csharp
private Email(string value)
```

Lý do:

```text
Không cho code bên ngoài bypass Email.Create(...).
```

### 11.4. Map Id thành lowercase

Hiện tại migration tạo:

```sql
"Id" uuid NOT NULL
```

Nên map thành:

```csharp
builder.Property(user => user.Id)
    .HasColumnName("id")
    .ValueGeneratedNever();
```

Lý do:

```text
PostgreSQL thường dùng lowercase snake_case.
Column id dễ query hơn "Id".
```

## 12. Tư Duy Cần Nhớ

Khi code backend theo Clean Architecture, luôn tự hỏi:

```text
Đây có phải business rule không?
Nếu có, đặt ở Domain.

Đây có phải use case không?
Nếu có, đặt ở Application.

Đây có phải database, security, external service không?
Nếu có, đặt ở Infrastructure.

Đây có phải HTTP request/response không?
Nếu có, đặt ở API.
```

Quy tắc quan trọng:

```text
Đừng để API quá thông minh.
Đừng để Application biết database.
Đừng để Domain biết EF Core.
Đừng tạo interface cho mọi thứ, chỉ tạo khi có behavior cần abstraction.
```

Feature register account hiện tại là version đầu tiên tốt để học. Nó đã có layering rõ ràng, domain model riêng, use case riêng, repository abstraction và infrastructure implementation. Bước tiếp theo nên là cải thiện error handling để API trả status code đúng hơn.
