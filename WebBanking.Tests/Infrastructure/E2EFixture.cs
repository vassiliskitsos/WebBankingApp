using System.Diagnostics;
using Microsoft.Playwright;

namespace WebBanking.Tests.Infrastructure;

// end to end tests
public sealed class E2EFixture : IAsyncLifetime
{
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
            await DisposeAsync();
            throw;
        }
    }

    private async Task StartAsync()
    {
        _publishFolder = Path.Combine(Path.GetTempPath(), $"webbanking-e2e-client-{Guid.NewGuid():N}");
        var clientFolder = await Task.Run(() => PublishClient(_publishFolder));

        _api = new E2EApiFactory(clientFolder);
        _api.UseKestrel(0);
        _api.StartServer();

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
