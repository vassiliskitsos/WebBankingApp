using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Repositories;

public sealed class UserRepository(AppDbContext db)
    : IUserRepository
{
    public Task<User?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return db.Users.SingleOrDefaultAsync(
            user => user.Username == username,
            cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return db.Users.AnyAsync(
            user => user.Username == username,
            cancellationToken);
    }

    public async Task<int> CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user.Id;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Users_Username"
            })
        {
            throw new DuplicateUsernameException(ex);
        }
    }
}
