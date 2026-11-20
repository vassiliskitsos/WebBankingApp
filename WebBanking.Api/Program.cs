using WebBanking.Api.Services;
using WebBanking.Api.Repositories;
using WebBanking.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebBanking.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// KAN-16: Registration services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


// Database. The connection string is read when the context is first resolved,
// so hosts that never touch the database (e.g. the e2e tests) start without one.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default must be configured.");
    }

    options.UseNpgsql(connectionString);
});

// JWT configuration
var jwtSecret = builder.Configuration["JWT_SECRET"];
var jwtIssuer = builder.Configuration["JWT_ISSUER"];

if (string.IsNullOrWhiteSpace(jwtSecret) ||
    Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JWT_SECRET must contain at least 32 UTF-8 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "JWT_ISSUER must be configured.");
}

// Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                // No audience is defined in the current API contract.
                ValidateAudience = false,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),

                RequireExpirationTime = true,
                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

// Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Χρειάζεται για το WebApplicationFactory<Program> στα integration tests.
public partial class Program;
