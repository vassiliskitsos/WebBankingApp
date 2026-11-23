using System.Security.Claims;
using WebBanking.Client.Services;
using WebBanking.Contracts.Auth;

namespace WebBanking.Tests.Tests;

public sealed class JwtAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationStateAsync_WithoutStoredSession_ReturnsAnonymousUser()
    {
        var tokenStore = new InMemoryAuthTokenStore();
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WithActiveSession_ReturnsAuthenticatedClaimsPrincipal()
    {
        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 7,
            Username: "theofania",
            Role: "Customer",
            Token: "valid-jwt-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("theofania", state.User.Identity?.Name);
        Assert.Equal("7", state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Customer", state.User.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public async Task NotifyAuthStateChanged_AfterLogin_FiresEventWithAuthenticatedState()
    {
        var tokenStore = new InMemoryAuthTokenStore();
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var notified = false;
        var isAuthenticatedAfterNotify = false;

        provider.AuthenticationStateChanged += async task =>
        {
            notified = true;
            var updatedState = await task;
            isAuthenticatedAfterNotify = updatedState.User.Identity?.IsAuthenticated ?? false;
        };

        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 7,
            Username: "theofania",
            Role: "Customer",
            Token: "valid-jwt-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        provider.NotifyAuthStateChanged();

        Assert.True(notified);
        Assert.True(isAuthenticatedAfterNotify);
    }

    [Fact]
    public async Task SignOutAsync_ClearsSessionAndNotifiesAnonymousState()
    {
        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 7,
            Username: "theofania",
            Role: "Customer",
            Token: "valid-jwt-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var isAuthenticatedAfterSignOut = true;
        provider.AuthenticationStateChanged += async task =>
        {
            var updatedState = await task;
            isAuthenticatedAfterSignOut = updatedState.User.Identity?.IsAuthenticated ?? false;
        };

        await provider.SignOutAsync();

        Assert.Null(tokenStore.CurrentSession);
        Assert.False(isAuthenticatedAfterSignOut);
    }

    private sealed class InMemoryAuthTokenStore : IAuthTokenStore
    {
        public AuthResponse? CurrentSession { get; private set; }

        public string? Token =>
            CurrentSession is not null && CurrentSession.ExpiresAtUtc > DateTime.UtcNow
                ? CurrentSession.Token
                : null;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task SetSessionAsync(AuthResponse session)
        {
            CurrentSession = session;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }
    }
}
