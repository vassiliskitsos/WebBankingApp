using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using WebBanking.Client.Services;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Tests.Tests;

public sealed class ApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task LoginAsync_ValidCredentials_SendsJsonReturnsAuthResponseAndStoresSession()
    {
        var expectedResponse = new AuthResponse(
            UserId: 7,
            Username: "theofania",
            Role: "Customer",
            Token: "jwt-token-123",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1));

        string? capturedBody = null;
        string? capturedContentType = null;
        AuthenticationHeaderValue? capturedAuth = null;

        var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            capturedContentType = request.Content?.Headers.ContentType?.MediaType;
            capturedAuth = request.Headers.Authorization;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse, options: JsonOptions)
            };
        });

        var tokenStore = new InMemoryAuthTokenStore();
        var navigation = new TestNavigationManager("http://localhost:5000/login");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.LoginAsync(new LoginRequest
        {
            Username = "theofania",
            Password = "Password123!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(expectedResponse.Token, result.Value.Token);
        Assert.Equal("application/json", capturedContentType);
        Assert.Null(capturedAuth);
        Assert.Contains("\"username\":\"theofania\"", capturedBody);
        Assert.Equal("jwt-token-123", tokenStore.Token);
        Assert.Empty(navigation.NavigatedUris);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsUnauthorizedResultWithoutRedirect()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(
                    new { Message = "Λάθος όνομα χρήστη ή κωδικός πρόσβασης." },
                    options: JsonOptions)
            }));

        var tokenStore = new InMemoryAuthTokenStore();
        var navigation = new TestNavigationManager("http://localhost:5000/login");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.LoginAsync(new LoginRequest
        {
            Username = "theofania",
            Password = "wrong"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.Equal("Λάθος όνομα χρήστη ή κωδικός πρόσβασης.", result.Error?.Message);
        Assert.Null(tokenStore.Token);
        Assert.Empty(navigation.NavigatedUris);
    }

    [Fact]
    public async Task GetAsync_ProtectedEndpointWithValidToken_AttachesBearerHeaderAndReturnsResult()
    {
        AuthenticationHeaderValue? capturedAuth = null;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedAuth = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SampleDto("OK"), options: JsonOptions)
            });
        });

        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            1,
            "theofania",
            "Customer",
            "active-jwt-token",
            DateTime.UtcNow.AddHours(1)));

        var navigation = new TestNavigationManager("http://localhost:5000/");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.GetAsync<SampleDto>("api/accounts");

        Assert.True(result.IsSuccess);
        Assert.Equal("OK", result.Value?.Status);
        Assert.NotNull(capturedAuth);
        Assert.Equal("Bearer", capturedAuth.Scheme);
        Assert.Equal("active-jwt-token", capturedAuth.Parameter);
        Assert.Empty(navigation.NavigatedUris);
    }

    [Fact]
    public async Task GetAsync_ProtectedEndpointReturns401_ClearsSessionRedirectsToLoginAndReturnsFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            1,
            "theofania",
            "Customer",
            "expired-on-server-token",
            DateTime.UtcNow.AddHours(1)));

        var navigation = new TestNavigationManager("http://localhost:5000/dashboard");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.GetAsync<SampleDto>("api/accounts");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.Null(tokenStore.Token);
        Assert.Equal(["/login"], navigation.NavigatedUris);
    }

    [Fact]
    public async Task GetAsync_ProtectedEndpointWithoutToken_RedirectsToLoginWithoutCallingServer()
    {
        var handlerCalled = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            handlerCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var tokenStore = new InMemoryAuthTokenStore();
        var navigation = new TestNavigationManager("http://localhost:5000/dashboard");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.GetAsync<SampleDto>("api/accounts");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.False(handlerCalled);
        Assert.Equal(["/login"], navigation.NavigatedUris);
    }

    [Fact]
    public async Task GetAsync_NetworkException_ReturnsFailureResultWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection refused"));

        var tokenStore = new InMemoryAuthTokenStore();
        var navigation = new TestNavigationManager("http://localhost:5000/login");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.GetAsync<SampleDto>("api/health", requiresAuth: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error?.Type);
        Assert.False(string.IsNullOrWhiteSpace(result.Error?.Message));
    }

    [Fact]
    public async Task GetAsync_MalformedJson_ReturnsUnprocessableResultWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-valid-json", Encoding.UTF8, "application/json")
            }));

        var tokenStore = new InMemoryAuthTokenStore();
        var navigation = new TestNavigationManager("http://localhost:5000/login");
        var client = CreateClient(handler, tokenStore, navigation);

        var result = await client.GetAsync<SampleDto>("api/health", requiresAuth: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error?.Type);
    }

    private static ApiClient CreateClient(
        HttpMessageHandler handler,
        IAuthTokenStore tokenStore,
        NavigationManager navigation)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5249/")
        };

        return new ApiClient(httpClient, tokenStore, navigation);
    }

    private sealed record SampleDto(string Status);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
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

    private sealed class TestNavigationManager : NavigationManager
    {
        public List<string> NavigatedUris { get; } = [];

        public TestNavigationManager(string initialUri)
        {
            Initialize("http://localhost:5000/", initialUri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            NavigatedUris.Add(uri);
        }
    }
}
