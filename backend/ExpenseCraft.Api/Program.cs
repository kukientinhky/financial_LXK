using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Application.Users;
using ExpenseCraft.Application.Users.Register;
using ExpenseCraft.Application.Users.Login;
using ExpenseCraft.Domain.Users;
using ExpenseCraft.Infrastructure.Persistence;
using ExpenseCraft.Infrastructure.Security;
using ExpenseCraft.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<LoginHandler>();
var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.Run();

public sealed record RegisterUserRequest(
    string Email,
    string Password);

public sealed record LoginUserRequest(
    string Email,
    string Password);
