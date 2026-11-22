using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebBanking.Client;
using WebBanking.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5249";

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});

builder.Services.AddScoped<IAuthTokenStore, AuthTokenStore>();
builder.Services.AddScoped<IApiClient, ApiClient>();

await builder.Build().RunAsync();
