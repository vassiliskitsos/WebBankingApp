using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services.Interfaces;

public interface ILoginService
{
    Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
