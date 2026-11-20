using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Repositories;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        User user,
        CancellationToken cancellationToken = default);
}
