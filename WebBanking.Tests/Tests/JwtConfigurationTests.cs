
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace WebBanking.Tests.Tests;

public class JwtConfigurationTests
{
    private const string ValidSecret =
        "test-only-secret-with-at-least-32-characters";

    [Fact]
    public void Startup_WithShortSecret_ThrowsException()
    {
        using var factory = CreateFactory(
            "short",
            "WebBankingApp.Tests");

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient());

        Assert.Contains("JWT_SECRET", exception.ToString());
    }

    [Fact]
    public void Startup_WithEmptyIssuer_ThrowsException()
    {
        using var factory = CreateFactory(
            ValidSecret,
            "");

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient());

        Assert.Contains("JWT_ISSUER", exception.ToString());
    }

    [Fact]
    public void Startup_WithInvalidIssuer_DoesNotLogSigningSecret()
    {
        using var logs = new RecordingLoggerProvider();
        using var factory = CreateFactory(ValidSecret, "", logs);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("JWT_ISSUER", exception.Message);
        Assert.DoesNotContain(ValidSecret, exception.ToString());
        Assert.All(logs.Entries, entry => Assert.DoesNotContain(ValidSecret, entry));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string secret,
        string issuer,
        ILoggerProvider? logs = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JWT_SECRET", secret);
                builder.UseSetting("JWT_ISSUER", issuer);
                if (logs is not null)
                {
                    builder.ConfigureLogging(logging => logging.AddProvider(logs));
                }
            });
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                entries.Enqueue($"{formatter(state, exception)} {exception}");
            }
        }
    }
}
