using Microsoft.AspNetCore.Identity;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Repositories;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services;

public sealed class LoginService(
    IUserRepository users,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService)
    : ILoginService
{
    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.GetByUsernameAsync(
            request.Username,
            cancellationToken);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        var response = jwtTokenService.GenerateToken(user);

        return Result<AuthResponse>.Ok(response);
    }

    private static Result<AuthResponse> InvalidCredentials()
    {
        return Result<AuthResponse>.Fail(
            new Error(
                ErrorType.Unauthorized,
                "Invalid username or password."));
    }
}
