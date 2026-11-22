using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services;

public interface IAccountService
{
    Task<Result<IReadOnlyList<AccountDto>>> GetAccountsAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
