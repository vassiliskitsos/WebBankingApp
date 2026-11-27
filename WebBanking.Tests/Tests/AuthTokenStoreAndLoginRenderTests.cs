using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using WebBanking.Client.Pages;
using WebBanking.Client.Services;
using WebBanking.Client.Services.Interfaces;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Tests.Tests;

public sealed class AuthTokenStoreAndLoginRenderTests
{
    [Fact]
    public async Task InitializeAsync_ConcurrentCalls_AwaitsSingleRestoreOperation()
    {
        var session = new AuthResponse(
            UserId: 7,
            Username: "theofania",
            Role: "Customer",
            Token: "restored-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1));

        var releaseRead = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var jsRuntime = new ControlledJsRuntime((_, _) => releaseRead.Task);
        var store = new AuthTokenStore(jsRuntime);

        var task1 = store.InitializeAsync();
        var task2 = store.InitializeAsync();

        releaseRead.SetResult(JsonSerializer.Serialize(session));

        await Task.WhenAll(task1, task2);

        Assert.Equal(1, jsRuntime.GetItemCallCount);
        Assert.Equal("restored-token", store.Token);
    }

    [Fact]
    public async Task InitializeAsync_MalformedSessionJson_ClearsStorageAndContinuesWithoutSession()
    {
        var jsRuntime = new ControlledJsRuntime((_, _) =>
            Task.FromResult<string?>("{not-valid-json"));

        var store = new AuthTokenStore(jsRuntime);

        await store.InitializeAsync();

        Assert.Null(store.CurrentSession);
        Assert.Null(store.Token);
        Assert.Contains("webbanking.auth.session", jsRuntime.RemovedKeys);
    }

    [Fact]
    public void LoginRazor_RendersWithClientDiRegistrations()
    {
        using var ctx = new BunitContext();

        ctx.Services.AddAuthorizationCore();
        ctx.Services.AddScoped<IAuthTokenStore, InMemoryAuthTokenStore>();
        ctx.Services.AddScoped<JwtAuthenticationStateProvider>();
        ctx.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<JwtAuthenticationStateProvider>());
        ctx.Services.AddScoped<IApiClient, StubApiClient>();

        var cut = ctx.Render<Login>();

        Assert.NotNull(cut.Find("form"));
        Assert.NotNull(cut.Find("input#username"));
        Assert.NotNull(cut.Find("input#password"));
    }

    private sealed class ControlledJsRuntime(
        Func<string, object?[]?, Task<string?>> onGetItem) : IJSRuntime
    {
        public int GetItemCallCount { get; private set; }
        public List<string> RemovedKeys { get; } = [];

        public async ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            object?[]? args)
        {
            if (identifier == "sessionStorage.getItem")
            {
                GetItemCallCount++;
                var result = await onGetItem(identifier, args);
                return (TValue)(object)result!;
            }

            if (identifier == "sessionStorage.removeItem" &&
                args is [string key, ..])
            {
                RemovedKeys.Add(key);
            }

            return default!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    private sealed class InMemoryAuthTokenStore : IAuthTokenStore
    {
        public AuthResponse? CurrentSession { get; private set; }
        public string? Token => CurrentSession?.Token;
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

    private sealed class StubApiClient : IApiClient
    {
        public Task<Result<AuthResponse>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AuthResponse>.Fail(
                new Error(ErrorType.Unauthorized, "Invalid")));

        public Task<Result<TResponse>> GetAsync<TResponse>(
            string uri,
            bool requiresAuth = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
            string uri,
            TRequest payload,
            bool requiresAuth = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> PostAsync<TRequest>(
            string uri,
            TRequest payload,
            bool requiresAuth = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
