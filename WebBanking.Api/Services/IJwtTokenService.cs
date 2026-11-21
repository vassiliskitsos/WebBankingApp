using WebBanking.Api.Data.Entities;
using WebBanking.Contracts.Auth;

namespace WebBanking.Api.Services;

public interface IJwtTokenService
{
    AuthResponse GenerateToken(User user);
}
