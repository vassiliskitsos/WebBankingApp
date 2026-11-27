using Microsoft.EntityFrameworkCore;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services.Interfaces;

namespace WebBanking.Api.Data.Repositories;

public sealed class AccountRepository(AppDbContext db)
    : IAccountRepository
{
    public async Task<IReadOnlyList<Account>> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await db.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.Id)
            .ToListAsync(cancellationToken);
    }
}
