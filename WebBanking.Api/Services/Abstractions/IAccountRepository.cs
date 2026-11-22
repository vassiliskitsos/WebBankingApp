using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Services.Abstractions;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
