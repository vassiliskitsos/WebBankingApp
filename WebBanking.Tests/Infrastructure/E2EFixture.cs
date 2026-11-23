using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;
using WebBanking.Api.Data;

namespace WebBanking.Tests.Infrastructure;

// end to end tests: real API, real PostgreSQL (Testcontainers), real browser
public sealed class E2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("webbanking_e2e")
        .WithUsername("webbanking")
        .WithPassword("test-only-password")
        .Build();

    private E2EApiFactory? _api;
    private IPlaywright? _playwright;
    private string? _publishFolder;

    public string ApiBaseUrl { get; private set; } = null!;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await StartAsync();
        }
        catch
        {
            // clean up whatever was started before the failure, including a partial publish
            try
            {
                await DisposeAsync();
            }
            catch
            {
                // a cleanup failure must not hide the original startup error
            }

            throw;
        }
    }

    private async Task StartAsync()
    {
        await _postgres.StartAsync();

        _publishFolder = Path.Combine(Path.GetTempPath(), $"webbanking-e2e-client-{Guid.NewGuid():N}");
        var clientFolder = await Task.Run(() => PublishClient(_publishFolder));

        _api = new E2EApiFactory(clientFolder, _postgres.GetConnectionString());
        _api.UseKestrel(0);
        _api.StartServer();

        await using (var scope = _api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        using var client = _api.CreateClient();
        ApiBaseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        // downloads chromium if not installed
        var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]));
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Η εγκατάσταση του Chromium απέτυχε (κωδικός {exitCode}).");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Browser is not null)
            {
                await Browser.DisposeAsync();
                Browser = null!;
            }

            _playwright?.Dispose();
            _playwright = null;

            if (_api is not null)
            {
                await _api.DisposeAsync();
                _api = null;
            }

            await _postgres.DisposeAsync();
        }
        finally
        {
            // the host is disposed first, so it no longer serves files from this folder
            if (_publishFolder is not null && Directory.Exists(_publishFolder))
            {
                Directory.Delete(_publishFolder, recursive: true);
            }

            _publishFolder = null;
        }
    }

    // the published client calls the dev API on localhost:5249; send those calls to this test server instead
    public async Task RouteDevApiToTestServerAsync(IBrowserContext context)
    {
        var corsHeaders = new Dictionary<string, string>
        {
            ["Access-Control-Allow-Origin"] = "*",
            ["Access-Control-Allow-Headers"] = "*",
            ["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS"
        };

        await context.RouteAsync("http://localhost:5249/**", async route =>
        {
            var request = route.Request;
            if (request.Method == HttpMethod.Options.Method)
            {
                await route.FulfillAsync(new() { Status = 204, Headers = corsHeaders });
                return;
            }

            using var message = new HttpRequestMessage(
                new HttpMethod(request.Method),
                ApiBaseUrl + new Uri(request.Url).PathAndQuery);

            if (request.PostDataBuffer is { } body)
            {
                message.Content = new ByteArrayContent(body);
                if (request.Headers.TryGetValue("content-type", out var contentType))
                {
                    message.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                }
            }

            if (request.Headers.TryGetValue("authorization", out var authorization))
            {
                message.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            using var http = new HttpClient();
            using var response = await http.SendAsync(message);

            var headers = new Dictionary<string, string>(corsHeaders)
            {
                ["Content-Type"] = response.Content.Headers.ContentType?.ToString() ?? "application/json"
            };

            await route.FulfillAsync(new()
            {
                Status = (int)response.StatusCode,
                Headers = headers,
                BodyBytes = await response.Content.ReadAsByteArrayAsync()
            });
        });
    }

    private static string PublishClient(string output)
    {
        var clientProject = Path.Combine(FindRepositoryRoot(), "WebBanking.Client");

        using var publish = Process.Start("dotnet", $"publish \"{clientProject}\" -c Debug -o \"{output}\" --nologo -v q")
            ?? throw new InvalidOperationException("Δεν ξεκίνησε το dotnet publish.");
        publish.WaitForExit();

        if (publish.ExitCode != 0)
        {
            throw new InvalidOperationException($"Το publish του client απέτυχε (κωδικός {publish.ExitCode}).");
        }

        return Path.Combine(output, "wwwroot");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(directory.FullName, "WebBanking.slnx")))
        {
            directory = directory.Parent
                ?? throw new DirectoryNotFoundException("Δεν βρέθηκε το WebBanking.slnx πάνω από τον φάκελο των tests.");
        }

        return directory.FullName;
    }
}

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Name = "e2e";
}
