using WebBanking.Api.Services.Abstractions;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services;

public sealed class AccountService(IAccountRepository accounts)
    : IAccountService
{
    public async Task<Result<IReadOnlyList<AccountDto>>> GetAccountsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await accounts.GetByUserIdAsync(
            userId,
            cancellationToken);

        var dtos = entities
            .Select(account => new AccountDto(
                account.Id,
                account.Iban,
                account.Balance,
                account.Currency))
            .ToList();

        return Result<IReadOnlyList<AccountDto>>.Ok(dtos);
    }
}
