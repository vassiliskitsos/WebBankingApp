using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using Scalar.AspNetCore;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Data.Repositories;
using WebBanking.Api.Repositories;
using WebBanking.Api.Repositories.Interfaces;
using WebBanking.Api.Services;
using WebBanking.Api.Services.Interfaces;

// Load the repo-root .env (local dev only; real environment variables take precedence).
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// API services
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    // Adds a "Bearer" scheme so Scalar shows an Authorize button, and marks [Authorize] endpoints as needing it.
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }

        return Task.CompletedTask;
    });
});

// CORS for Blazor WebAssembly client
const string ClientCorsPolicy = "ClientCorsPolicy";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5018", "https://localhost:7198"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Authentication & domain services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Database. The connection string is read when the context is first resolved,
// so hosts that never touch the database (e.g. the e2e tests) start without one.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default")
        ?? BuildLocalConnectionString(configuration);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default or POSTGRES_* settings (.env) must be configured.");
    }

    options.UseNpgsql(connectionString);
});

// Local dev fallback: build the connection string from the POSTGRES_* values in .env.
static string? BuildLocalConnectionString(IConfiguration configuration)
{
    var database = configuration["POSTGRES_DB"];
    var username = configuration["POSTGRES_USER"];
    var password = configuration["POSTGRES_PASSWORD"];

    if (string.IsNullOrWhiteSpace(database) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        return null;
    }

    return new NpgsqlConnectionStringBuilder
    {
        Host = "localhost",
        Port = int.TryParse(configuration["POSTGRES_PORT"], out var port) ? port : 5432,
        Database = database,
        Username = username,
        Password = password
    }.ConnectionString;
}

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
    app.MapScalarApiReference();
}

app.UseCors(ClientCorsPolicy);

// The Development HTTP client cannot follow a cross-origin redirect without losing Authorization.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Χρειάζεται για το WebApplicationFactory<Program> στα integration tests.
public partial class Program;
