using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services.Interfaces;

public interface IRegistrationService
{
    Task<Result<int>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);
}
