using WebBanking.Api.Data.Entities;
using WebBanking.Contracts.Auth;

namespace WebBanking.Api.Services.Interfaces;

public interface IJwtTokenService
{
    AuthResponse GenerateToken(User user);
}
