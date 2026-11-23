using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebBanking.Client;
using WebBanking.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var defaultApiBaseUrl = builder.HostEnvironment.BaseAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
    ? "https://localhost:7211"
    : "http://localhost:5249";

// wwwroot/appsettings.json must exist so the framework loads it; the E2E harness overwrites ApiBaseUrl there
var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl) ? defaultApiBaseUrl : configuredApiBaseUrl;

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IAuthTokenStore, AuthTokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<IApiClient, ApiClient>();

await builder.Build().RunAsync();
