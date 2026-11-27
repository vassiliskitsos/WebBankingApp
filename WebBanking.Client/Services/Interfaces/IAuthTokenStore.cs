using WebBanking.Contracts.Auth;

namespace WebBanking.Client.Services.Interfaces;

public interface IAuthTokenStore
{
    AuthResponse? CurrentSession { get; }
    string? Token { get; }
    Task InitializeAsync();
    Task SetSessionAsync(AuthResponse session);
    Task ClearAsync();
}
