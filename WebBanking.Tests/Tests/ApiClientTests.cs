using System.Net;
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
    [Fact]
    public async Task LoginAsync_ValidCredentials_SendsJsonPayloadAndStoresSession()
    {
        string? capturedBody = null;
        string? capturedContentType = null;

        var expectedResponse = new AuthResponse(
            UserId: 12,
            Username: "theofania",
            Role: "Customer",
            Token: "jwt-token-123",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1));

        var handler = new StubHttpMessageHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost:5249/api/auth/login", request.RequestUri?.ToString());
            Assert.Null(request.Headers.Authorization);

            capturedContentType = request.Content?.Headers.ContentType?.MediaType;
            capturedBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };
        });

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/login");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.LoginAsync(new LoginRequest
        {
            Username = "theofania",
            Password = "Password123!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("theofania", result.Value.Username);
        Assert.Equal("jwt-token-123", tokenStore.Token);
        Assert.Equal("application/json", capturedContentType);
        Assert.Contains("\"username\":\"theofania\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"Password123!\"", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginAsync_UnauthorizedResponse_MapsApiErrorWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { message = "Λάθος όνομα χρήστη ή κωδικός πρόσβασης." })
            });
        });

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/login");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.LoginAsync(new LoginRequest
        {
            Username = "wronguser",
            Password = "WrongPassword1!"
        });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Λάθος όνομα χρήστη ή κωδικός πρόσβασης.", result.Error.Message);
        Assert.Equal("http://localhost:5018/login", nav.LastNavigatedUri);
    }

    [Fact]
    public async Task GetAsync_ProtectedEndpointWithStoredToken_AttachesBearerAuthorizationHeader()
    {
        string? authorizationHeader = null;

        var handler = new StubHttpMessageHandler(request =>
        {
            authorizationHeader = request.Headers.Authorization?.ToString();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "ok" })
            });
        });

        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 5,
            Username: "theofania",
            Role: "Customer",
            Token: "secret-bearer-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        var nav = new TestNavigationManager("http://localhost:5018/");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.GetAsync<JsonElement>("api/accounts/me");

        Assert.True(result.IsSuccess);
        Assert.Equal("Bearer secret-bearer-token", authorizationHeader);
    }

    [Fact]
    public async Task GetAsync_AfterSignOut_SendsNoRequestWithBearerToken()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "ok" })
            });
        });

        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 5,
            Username: "theofania",
            Role: "Customer",
            Token: "secret-bearer-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        var authStateProvider = new JwtAuthenticationStateProvider(tokenStore);
        var nav = new TestNavigationManager("http://localhost:5018/accounts");
        var client = CreateClient(handler, tokenStore, nav, authStateProvider);

        await authStateProvider.SignOutAsync();
        var result = await client.GetAsync<JsonElement>("api/accounts/me");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, requestCount);
        Assert.Null(tokenStore.Token);
    }

    [Fact]
    public async Task GetAsync_UnauthorizedOnProtectedEndpoint_ClearsSessionNotifiesProviderAndRedirectsToLogin()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 5,
            Username: "theofania",
            Role: "Customer",
            Token: "expired-or-revoked-token",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        var authStateProvider = new JwtAuthenticationStateProvider(tokenStore);
        var stateChangedNotified = false;
        authStateProvider.AuthenticationStateChanged += _ => stateChangedNotified = true;

        var nav = new TestNavigationManager("http://localhost:5018/accounts");
        var client = CreateClient(handler, tokenStore, nav, authStateProvider);

        var result = await client.GetAsync<JsonElement>("api/accounts/me");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error?.Type);
        Assert.Null(tokenStore.Token);
        Assert.True(stateChangedNotified);
        Assert.Equal("/login", nav.LastNavigatedUri);
    }

    [Fact]
    public async Task GetAsync_LateUnauthorizedFromOlderToken_DoesNotClearNewerSession()
    {
        var tokenStore = new InMemoryAuthTokenStore();
        await tokenStore.SetSessionAsync(new AuthResponse(
            UserId: 5,
            Username: "theofania",
            Role: "Customer",
            Token: "token-A",
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

        var handler = new StubHttpMessageHandler(async _ =>
        {
            await tokenStore.SetSessionAsync(new AuthResponse(
                UserId: 5,
                Username: "theofania",
                Role: "Customer",
                Token: "token-B",
                ExpiresAtUtc: DateTime.UtcNow.AddHours(1)));

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var authStateProvider = new JwtAuthenticationStateProvider(tokenStore);
        var stateChangedNotified = false;
        authStateProvider.AuthenticationStateChanged += _ => stateChangedNotified = true;

        var nav = new TestNavigationManager("http://localhost:5018/accounts");
        var client = CreateClient(handler, tokenStore, nav, authStateProvider);

        var result = await client.GetAsync<JsonElement>("api/accounts/me");

        Assert.False(result.IsSuccess);
        Assert.Equal("token-B", tokenStore.Token);
        Assert.False(stateChangedNotified);
        Assert.Equal("http://localhost:5018/accounts", nav.LastNavigatedUri);
    }

    [Fact]
    public async Task PostAsync_BodyFreeSuccess_DisposesHttpResponseMessage()
    {
        var trackingContent = new TrackingStringContent("{}");
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = trackingContent
            }));

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.PostAsync("api/test/ping", new { ok = true }, requiresAuth: false);

        Assert.True(result.IsSuccess);
        Assert.True(trackingContent.IsDisposed);
    }

    [Fact]
    public async Task GetAsync_NetworkException_ReturnsFailureResultWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("Connection refused"));

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/login");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.GetAsync<JsonElement>("api/accounts/me", requiresAuth: false);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Unprocessable, result.Error.Type);
        Assert.Contains("Αδυναμία επικοινωνίας", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_InvalidJsonPayload_ReturnsFailureResultWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-valid-json", Encoding.UTF8, "application/json")
            }));

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/accounts");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.GetAsync<JsonElement>("api/accounts/me", requiresAuth: false);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Unprocessable, result.Error.Type);
    }

    [Fact]
    public async Task PostAsync_BadRequestWithNonJsonBody_MapsToValidationErrorWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Bad request plain text", Encoding.UTF8, "text/plain")
            }));

        var tokenStore = new InMemoryAuthTokenStore();
        var nav = new TestNavigationManager("http://localhost:5018/login");
        var client = CreateClient(handler, tokenStore, nav);

        var result = await client.PostAsync("api/test", new { name = "x" }, requiresAuth: false);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    private static ApiClient CreateClient(
        HttpMessageHandler handler,
        IAuthTokenStore tokenStore,
        NavigationManager navigationManager,
        JwtAuthenticationStateProvider? authStateProvider = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5249", UriKind.Absolute)
        };

        return new ApiClient(httpClient, tokenStore, navigationManager, authStateProvider);
    }

    private sealed class TrackingStringContent(string content)
        : StringContent(content, Encoding.UTF8, "application/json")
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
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
        public string LastNavigatedUri { get; private set; }

        public TestNavigationManager(string initialUri)
        {
            Initialize("http://localhost:5018/", initialUri);
            LastNavigatedUri = initialUri;
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastNavigatedUri = uri;
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
