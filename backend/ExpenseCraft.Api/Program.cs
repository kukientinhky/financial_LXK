using ExpenseCraft.Application.Chat;
using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Application.Transactions;
using ExpenseCraft.Application.Transactions.Analytics;
using ExpenseCraft.Application.Transactions.Create;
using ExpenseCraft.Application.Transactions.Delete;
using ExpenseCraft.Application.Transactions.Get;
using ExpenseCraft.Application.Users;
using ExpenseCraft.Application.Users.Login;
using ExpenseCraft.Application.Users.Register;
using ExpenseCraft.Infrastructure.Chat;
using ExpenseCraft.Infrastructure.Persistence;
using ExpenseCraft.Infrastructure.Security;
using ExpenseCraft.Infrastructure.Transactions;
using ExpenseCraft.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;


var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt settings are not configured.");

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSettings.Secret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenProvider, JwtTokenProvider>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<CreateTransactionHandler>();
builder.Services.AddScoped<GetTransactionsHandler>();
builder.Services.AddScoped<DeleteTransactionHandler>();
builder.Services.AddScoped<GetAnalyticsSummaryHandler>();
builder.Services.AddScoped<GetChatMessagesHandler>();
builder.Services.AddScoped<SaveChatMessageHandler>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ArgumentException ex)
    {
        await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Invalid request", ex.Message);
    }
    catch (KeyNotFoundException ex)
    {
        await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not found", ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Invalid operation", ex.Message);
    }
});

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
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

app.MapPost("/api/users/login", async (
    LoginUserRequest request,
    LoginHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new LoginUserCommand(request.Email, request.Password),
        cancellationToken);

    return Results.Ok(result);
})
.WithName("LoginUser")
.WithOpenApi();

var authorizedApi = app.MapGroup("/api").RequireAuthorization();

authorizedApi.MapGet("/users/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
        Email = user.FindFirstValue(ClaimTypes.Email)
    });
})
.WithName("GetCurrentUser")
.WithOpenApi();

authorizedApi.MapPost("/agent/tools/income", async (
    AgentTransactionRequest request,
    ClaimsPrincipal user,
    CreateTransactionHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new CreateTransactionCommand(
            userId,
            "income",
            request.Amount,
            request.Currency,
            request.Category,
            request.Note,
            DefaultAgentSource(request.Source),
            request.OccurredAt),
        cancellationToken);

    return Results.Created($"/api/transactions/{result.Id}", result);
})
.WithName("CreateIncomeFromAgentTool")
.WithOpenApi();

authorizedApi.MapPost("/agent/tools/expense", async (
    AgentTransactionRequest request,
    ClaimsPrincipal user,
    CreateTransactionHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new CreateTransactionCommand(
            userId,
            "expense",
            request.Amount,
            request.Currency,
            request.Category,
            request.Note,
            DefaultAgentSource(request.Source),
            request.OccurredAt),
        cancellationToken);

    return Results.Created($"/api/transactions/{result.Id}", result);
})
.WithName("CreateExpenseFromAgentTool")
.WithOpenApi();

authorizedApi.MapGet("/transactions", async (
    int? limit,
    ClaimsPrincipal user,
    GetTransactionsHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new GetTransactionsQuery(userId, limit ?? 20),
        cancellationToken);

    return Results.Ok(result);
})
.WithName("GetTransactions")
.WithOpenApi();

authorizedApi.MapDelete("/transactions/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    DeleteTransactionHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var deleted = await handler.HandleAsync(
        new DeleteTransactionCommand(userId, id),
        cancellationToken);

    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteTransaction")
.WithOpenApi();

authorizedApi.MapGet("/analytics/summary", async (
    DateOnly from,
    DateOnly to,
    ClaimsPrincipal user,
    GetAnalyticsSummaryHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new GetAnalyticsSummaryQuery(userId, from, to),
        cancellationToken);

    return Results.Ok(result);
})
.WithName("GetAnalyticsSummary")
.WithOpenApi();

authorizedApi.MapGet("/chat/messages", async (
    int? limit,
    ClaimsPrincipal user,
    GetChatMessagesHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new GetChatMessagesQuery(userId, limit ?? 50),
        cancellationToken);

    return Results.Ok(result);
})
.WithName("GetChatMessages")
.WithOpenApi();

authorizedApi.MapPost("/chat/messages", async (
    SaveChatMessageRequest request,
    ClaimsPrincipal user,
    SaveChatMessageHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await handler.HandleAsync(
        new SaveChatMessageCommand(
            userId,
            request.Role,
            request.Content,
            request.Intent,
            request.TransactionId),
        cancellationToken);

    return Results.Created($"/api/chat/messages/{result.Id}", result);
})
.WithName("SaveChatMessage")
.WithOpenApi();

static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(userIdValue, out userId);
}

static string DefaultAgentSource(string? source)
{
    return string.IsNullOrWhiteSpace(source) ? "agent" : source;
}

static Task WriteProblemAsync(
    HttpContext context,
    int statusCode,
    string title,
    string detail)
{
    if (context.Response.HasStarted)
    {
        throw new InvalidOperationException("Cannot write problem response because the response has already started.");
    }

    context.Response.Clear();

    return Results.Problem(
        title: title,
        detail: detail,
        statusCode: statusCode)
        .ExecuteAsync(context);
}

app.Run();

public sealed record RegisterUserRequest(
    string Email,
    string Password);

public sealed record LoginUserRequest(
    string Email,
    string Password);

public sealed record AgentTransactionRequest(
    decimal Amount,
    string? Currency,
    string Category,
    string? Note,
    string? Source,
    DateTimeOffset? OccurredAt);

public sealed record SaveChatMessageRequest(
    string Role,
    string Content,
    string? Intent,
    Guid? TransactionId);
