using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services.Interfaces;
using WebBanking.Contracts.Auth;

namespace WebBanking.Api.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly int _expiresMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "JWT_SECRET must be configured.");

        if (Encoding.UTF8.GetByteCount(_secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT_SECRET must contain at least 32 UTF-8 bytes.");
        }

        _issuer = configuration["JWT_ISSUER"]
            ?? throw new InvalidOperationException(
                "JWT_ISSUER must be configured.");

        if (string.IsNullOrWhiteSpace(_issuer))
        {
            throw new InvalidOperationException(
                "JWT_ISSUER must be configured.");
        }

        if (!int.TryParse(
                configuration["JWT_EXPIRES_MINUTES"],
                out _expiresMinutes) ||
            _expiresMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT_EXPIRES_MINUTES must be a positive integer.");
        }
    }

    public AuthResponse GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_expiresMinutes);

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.Username),

            new Claim(
                ClaimTypes.Role,
                user.Role.ToString())
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_secret));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _issuer,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler()
            .WriteToken(jwt);

        return new AuthResponse(
            user.Id,
            user.Username,
            user.Role.ToString(),
            token,
            expiresAtUtc);
    }
}
