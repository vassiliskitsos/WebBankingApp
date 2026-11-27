using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Services.Interfaces;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
