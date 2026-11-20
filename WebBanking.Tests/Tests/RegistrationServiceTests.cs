using Microsoft.AspNetCore.Identity;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Repositories;
using WebBanking.Api.Services;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Tests.Tests;

public sealed class RegistrationServiceTests
{
    private static RegistrationService CreateService(
        FakeUserRepository repository)
    {
        return new RegistrationService(
            repository,
            new PasswordHasher<User>());
    }

    private static RegisterRequest ValidRequest() => new()
    {
        Username = "sara",
        Password = "SecurePassword123!"
    };

    [Fact]
    public async Task Register_ValidUser_ReturnsSuccessAndId()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Null(result.Error);
        Assert.Single(repository.Users);
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);
        var request = ValidRequest();

        await service.RegisterAsync(request);

        var saved = Assert.Single(repository.Users);

        Assert.NotEqual(request.Password, saved.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));

        var verification = new PasswordHasher<User>()
            .VerifyHashedPassword(
                saved,
                saved.PasswordHash,
                request.Password);

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            verification);
    }

    [Fact]
    public async Task Register_AssignsCustomerRole()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        await service.RegisterAsync(ValidRequest());

        Assert.Equal(
            Role.Customer,
            Assert.Single(repository.Users).Role);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);
        var request = ValidRequest();

        var first = await service.RegisterAsync(request);
        var second = await service.RegisterAsync(request);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorType.Conflict, second.Error?.Type);
        Assert.Single(repository.Users);
    }

    [Fact]
    public async Task Register_DuplicateDuringSave_ReturnsConflict()
    {
        var repository = new FakeUserRepository
        {
            SimulateDuplicateDuringSave = true
        };

        var service = CreateService(repository);

        var result = await service.RegisterAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Empty(repository.Users);
    }

    [Theory]
    [InlineData("", "SecurePassword123!")]
    [InlineData("ab", "SecurePassword123!")]
    [InlineData("sara", "12345")]
    [InlineData("sara", "")]
    public async Task Register_InvalidInput_ReturnsValidationError(
        string username,
        string password)
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(
            new RegisterRequest
            {
                Username = username,
                Password = password
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Empty(repository.Users);
    }

    [Fact]
    public async Task Register_NullRequest_ThrowsArgumentNullException()
    {
        var service = CreateService(new FakeUserRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RegisterAsync(null!));
    }

    [Fact]
    public async Task Register_UnexpectedRepositoryFailure_Propagates()
    {
        var repository = new FakeUserRepository
        {
            SimulateUnexpectedFailure = true
        };

        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterAsync(ValidRequest()));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public bool SimulateDuplicateDuringSave { get; set; }

        public bool SimulateUnexpectedFailure { get; set; }

        public Task<bool> UsernameExistsAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Users.Any(u => u.Username == username));
        }

        public Task<int> CreateAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            if (SimulateUnexpectedFailure)
            {
                throw new InvalidOperationException(
                    "Simulated database failure.");
            }

            if (SimulateDuplicateDuringSave ||
                Users.Any(u => u.Username == user.Username))
            {
                throw new DuplicateUsernameException(
                    new InvalidOperationException(
                        "Simulated unique constraint violation."));
            }

            user.Id = Users.Count + 1;
            Users.Add(user);

            return Task.FromResult(user.Id);
        }
    }
}
