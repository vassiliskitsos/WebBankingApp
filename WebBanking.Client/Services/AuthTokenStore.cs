using System.Text.Json;
using Microsoft.JSInterop;
using WebBanking.Contracts.Auth;

namespace WebBanking.Client.Services;

public interface IAuthTokenStore
{
    AuthResponse? CurrentSession { get; }
    string? Token { get; }
    Task InitializeAsync();
    Task SetSessionAsync(AuthResponse session);
    Task ClearAsync();
}

public sealed class AuthTokenStore(IJSRuntime jsRuntime) : IAuthTokenStore
{
    private const string StorageKey = "webbanking.auth.session";
    private bool _initialized;

    public AuthResponse? CurrentSession { get; private set; }

    public string? Token =>
        CurrentSession is not null && CurrentSession.ExpiresAtUtc > DateTime.UtcNow
            ? CurrentSession.Token
            : null;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            var json = await jsRuntime.InvokeAsync<string?>(
                "sessionStorage.getItem",
                StorageKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var session = JsonSerializer.Deserialize<AuthResponse>(json);

            if (session is not null && session.ExpiresAtUtc > DateTime.UtcNow)
            {
                CurrentSession = session;
            }
            else
            {
                await ClearAsync();
            }
        }
        catch (JSException)
        {
            // Ignore JS interop errors when running outside a browser context.
        }
    }

    public async Task SetSessionAsync(AuthResponse session)
    {
        ArgumentNullException.ThrowIfNull(session);

        CurrentSession = session;
        _initialized = true;

        try
        {
            var json = JsonSerializer.Serialize(session);
            await jsRuntime.InvokeVoidAsync(
                "sessionStorage.setItem",
                StorageKey,
                json);
        }
        catch (JSException)
        {
            // Keep in-memory session even if browser storage is unavailable.
        }
    }

    public async Task ClearAsync()
    {
        CurrentSession = null;

        try
        {
            await jsRuntime.InvokeVoidAsync(
                "sessionStorage.removeItem",
                StorageKey);
        }
        catch (JSException)
        {
            // Ignore JS interop errors when browser storage is unavailable.
        }
    }
}
