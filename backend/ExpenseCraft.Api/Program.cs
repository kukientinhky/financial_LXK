using ExpenseCraft.Application.Common.Security;
using ExpenseCraft.Application.Users;
using ExpenseCraft.Application.Users.Register;
using ExpenseCraft.Application.Users.Login;
using ExpenseCraft.Domain.Users;
using ExpenseCraft.Infrastructure.Persistence;
using ExpenseCraft.Infrastructure.Security;
using ExpenseCraft.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenProvider, JwtTokenProvider>();
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

app.MapGet("/api/users/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
        Email = user.FindFirstValue(ClaimTypes.Email)
    });
})
.RequireAuthorization()
.WithName("GetCurrentUser")
.WithOpenApi();

app.Run();

public sealed record RegisterUserRequest(
    string Email,
    string Password);

public sealed record LoginUserRequest(
    string Email,
    string Password);
