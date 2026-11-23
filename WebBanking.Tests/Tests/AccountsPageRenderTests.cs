using Bunit;
using Microsoft.Extensions.DependencyInjection;
using WebBanking.Client.Pages;
using WebBanking.Client.Services;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Tests.Tests;

public sealed class AccountsPageRenderTests
{
    private static readonly AccountDto Account = new(
        Id: 1,
        Iban: "GR1601101250000000012300695",
        Balance: 1234.5m,
        Currency: "EUR");

    [Fact]
    public void Render_WhileRequestPending_ShowsLoadingState()
    {
        using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("probeuser");
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(
            () => new TaskCompletionSource<Result<IReadOnlyList<AccountDto>>>().Task));

        var cut = ctx.Render<AccountsPage>();

        Assert.Contains("Φόρτωση λογαριασμών", cut.Markup);
    }

    [Fact]
    public void Render_WithAccounts_ShowsIbanBalanceAndCurrency()
    {
        using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("probeuser");
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(() =>
            Task.FromResult(Result<IReadOnlyList<AccountDto>>.Ok([Account]))));

        var cut = ctx.Render<AccountsPage>();

        var row = cut.WaitForElement("tbody tr");
        Assert.Contains(Account.Iban, row.TextContent);
        Assert.Contains("1,234.50", row.TextContent);
        Assert.Contains("EUR", row.TextContent);
        Assert.Single(cut.FindAll(".card"));
    }

    [Fact]
    public void Render_WithNoAccounts_ShowsEmptyState()
    {
        using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("probeuser");
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(() =>
            Task.FromResult(Result<IReadOnlyList<AccountDto>>.Ok([]))));

        var cut = ctx.Render<AccountsPage>();

        cut.WaitForState(() => cut.Markup.Contains("Δεν βρέθηκαν λογαριασμοί"));
        Assert.Empty(cut.FindAll("tbody tr"));
    }

    [Fact]
    public void Render_WhenUnauthorized_ShowsSessionExpiredWithoutRetry()
    {
        using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("probeuser");
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(() =>
            Task.FromResult(Result<IReadOnlyList<AccountDto>>.Fail(
                new Error(ErrorType.Unauthorized, "Unauthorized")))));

        var cut = ctx.Render<AccountsPage>();

        cut.WaitForState(() => cut.Markup.Contains("Η συνεδρία σας έχει λήξει"));
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void Render_WhenRequestFails_ShowsErrorWithRetry()
    {
        using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("probeuser");
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(() =>
            Task.FromResult(Result<IReadOnlyList<AccountDto>>.Fail(
                new Error(ErrorType.Unprocessable, "Σφάλμα διακομιστή")))));

        var cut = ctx.Render<AccountsPage>();

        cut.WaitForState(() => cut.Markup.Contains("Σφάλμα διακομιστή"));
        Assert.NotNull(cut.Find("button"));
    }

    [Fact]
    public void SignIn_WhileOnAccountsPage_LoadsAccounts()
    {
        using var ctx = new BunitContext();
        var auth = ctx.AddAuthorization();
        auth.SetNotAuthorized();
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(() =>
            Task.FromResult(Result<IReadOnlyList<AccountDto>>.Ok([Account]))));

        var cut = ctx.Render<AccountsPage>();
        auth.SetAuthorized("probeuser");

        cut.WaitForElement("tbody tr");
        Assert.Contains(Account.Iban, cut.Markup);
    }

    [Fact]
    public void SignIn_WhenLoadThrows_ShowsErrorInsteadOfSpinner()
    {
        using var ctx = new BunitContext();
        var auth = ctx.AddAuthorization();
        auth.SetNotAuthorized();
        ctx.Services.AddScoped<IApiClient>(_ => new StubApiClient(
            () => throw new InvalidOperationException("boom")));

        var cut = ctx.Render<AccountsPage>();
        auth.SetAuthorized("probeuser");

        cut.WaitForState(() => cut.Markup.Contains("Δεν ήταν δυνατή η φόρτωση"));
        Assert.DoesNotContain("Φόρτωση λογαριασμών", cut.Markup);
    }

    private sealed class StubApiClient(
        Func<Task<Result<IReadOnlyList<AccountDto>>>> getAccounts) : IApiClient
    {
        public Task<Result<AuthResponse>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<Result<TResponse>> GetAsync<TResponse>(
            string uri,
            bool requiresAuth = true,
            CancellationToken cancellationToken = default)
        {
            var result = await getAccounts();
            return result.IsSuccess
                ? Result<TResponse>.Ok((TResponse)(object)result.Value!)
                : Result<TResponse>.Fail(result.Error!);
        }

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
