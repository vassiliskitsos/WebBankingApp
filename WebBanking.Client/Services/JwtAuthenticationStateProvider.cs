using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace WebBanking.Client.Services;

public sealed class JwtAuthenticationStateProvider(IAuthTokenStore tokenStore)
    : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal AnonymousUser = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await tokenStore.InitializeAsync();

        var session = tokenStore.CurrentSession;
        if (session is null || string.IsNullOrWhiteSpace(tokenStore.Token))
        {
            return new AuthenticationState(AnonymousUser);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.Username),
            new(ClaimTypes.Role, session.Role)
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await tokenStore.ClearAsync();
        NotifyAuthStateChanged();
    }
}
