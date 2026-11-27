using Microsoft.AspNetCore.Identity;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Repositories;
using WebBanking.Api.Repositories.Interfaces;
using WebBanking.Api.Services;
using WebBanking.Api.Services.Interfaces;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Tests.Tests;

public sealed class LoginServiceTests
{
    private const string Password = "ValidPassword123!";

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokenForVerifiedUser()
    {
        var user = new User { Id = 7, Username = "customer" };
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, Password);
        var tokens = new RecordingTokenService();
        var service = new LoginService(new SingleUserRepository(user), hasher, tokens);

        var result = await service.LoginAsync(new LoginRequest
        {
            Username = user.Username,
            Password = Password
        });

        Assert.True(result.IsSuccess);
        Assert.Same(tokens.Response, result.Value);
        Assert.Same(user, Assert.Single(tokens.Users));
    }

    [Theory]
    [InlineData("customer", "WrongPassword123!")]
    [InlineData("unknown", Password)]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedWithoutIssuingToken(
        string username, string password)
    {
        var user = new User { Id = 7, Username = "customer" };
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, Password);
        var tokens = new RecordingTokenService();
        var service = new LoginService(new SingleUserRepository(user), hasher, tokens);

        var result = await service.LoginAsync(new LoginRequest
        {
            Username = username,
            Password = password
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.Equal("Invalid username or password.", result.Error?.Message);
        Assert.Empty(tokens.Users);
    }

    private sealed class SingleUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(username == user.Username ? user : null);

        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTokenService : IJwtTokenService
    {
        public List<User> Users { get; } = [];
        public AuthResponse Response { get; } = new(7, "customer", "Customer", "test-token", DateTime.UnixEpoch);

        public AuthResponse GenerateToken(User user)
        {
            Users.Add(user);
            return Response;
        }
    }
}
