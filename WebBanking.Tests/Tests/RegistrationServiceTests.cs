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
        var request = ValidRequest();

        var result = await service.RegisterAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Null(result.Error);

        var saved = Assert.Single(repository.Users);
        Assert.Equal(request.Username, saved.Username);
    }

    [Theory]
    [InlineData("abc", "123456")]
    [InlineData("user_with_50_chars_1234567890123456789012345678901", "ValidPass1!")]
    public async Task Register_BoundaryValidInputs_ReturnsSuccess(
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

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var saved = Assert.Single(repository.Users);
        Assert.Equal(username, saved.Username);
        Assert.Equal(Role.Customer, saved.Role);
    }

    [Fact]
    public async Task Register_MultipleValidUsers_AssignsSequentialIdsAndDistinctHashes()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var first = await service.RegisterAsync(new RegisterRequest
        {
            Username = "alice",
            Password = "SamePassword123!"
        });

        var second = await service.RegisterAsync(new RegisterRequest
        {
            Username = "bob",
            Password = "SamePassword123!"
        });

        Assert.True(first.IsSuccess);
        Assert.Equal(1, first.Value);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value);
        Assert.Equal(2, repository.Users.Count);
        Assert.NotEqual(
            repository.Users[0].PasswordHash,
            repository.Users[1].PasswordHash);
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
        Assert.False(string.IsNullOrWhiteSpace(second.Error?.Message));
        Assert.Single(repository.Users);
        Assert.Equal(1, repository.CreateCalls);
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
        Assert.False(string.IsNullOrWhiteSpace(result.Error?.Message));
        Assert.Empty(repository.Users);
    }

    [Theory]
    [InlineData("", "SecurePassword123!")]
    [InlineData("   ", "SecurePassword123!")]
    [InlineData("ab", "SecurePassword123!")]
    [InlineData("user_exceeding_50_chars_12345678901234567890123456789", "SecurePassword123!")]
    [InlineData("sara", "")]
    [InlineData("sara", "     ")]
    [InlineData("sara", "12345")]
    public async Task Register_InvalidInput_ReturnsValidationErrorWithoutCallingRepository(
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
        Assert.False(string.IsNullOrWhiteSpace(result.Error?.Message));
        Assert.Empty(repository.Users);
        Assert.Equal(0, repository.UsernameExistsCalls);
        Assert.Equal(0, repository.CreateCalls);
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

        public int UsernameExistsCalls { get; private set; }

        public int CreateCalls { get; private set; }

        public Task<User?> GetByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Users.SingleOrDefault(u => u.Username == username));
        }

        public Task<bool> UsernameExistsAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            UsernameExistsCalls++;
            return Task.FromResult(
                Users.Any(u => u.Username == username));
        }

        public Task<int> CreateAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;

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
