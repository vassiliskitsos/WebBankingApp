using Microsoft.Playwright;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class RegistrationJourneyE2ETests(E2EFixture fixture)
{
    [Fact]
    public async Task RegisterThenLogin_CanOpenAccountsForNewUser()
    {
        var username = $"e2e{Guid.NewGuid():N}"[..20];
        const string password = "BrowserTest123!";

        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);

        await page.GotoAsync($"{fixture.ApiBaseUrl}/register");
        await page.GetByLabel("Όνομα Χρήστη (Username)").FillAsync(username);
        await page.GetByLabel("Κωδικός Πρόσβασης", new() { Exact = true }).FillAsync(password);
        await page.GetByLabel("Επιβεβαίωση Κωδικού").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Εγγραφή", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Alert))
            .ToHaveTextAsync($"Ο λογαριασμός για τον χρήστη '{username}' δημιουργήθηκε επιτυχώς!",
                new() { Timeout = 30_000 });

        await page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Σύνδεση", Exact = true })
            .ClickAsync();
        await page.WaitForURLAsync("**/login");
        await page.GetByLabel("Όνομα Χρήστη (Username)").FillAsync(username);
        await page.GetByLabel("Κωδικός Πρόσβασης", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Σύνδεση", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading,
                new() { Name = $"Καλώς ήρθατε, {username}", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        var accountsResponse = await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Navigation)
                .GetByRole(AriaRole.Link, new() { Name = "Λογαριασμοί", Exact = true }).ClickAsync(),
            response => response.Url == $"{fixture.ApiBaseUrl}/api/accounts" && response.Request.Method == "GET");

        Assert.Equal(200, accountsResponse.Status);
        await page.WaitForURLAsync("**/accounts");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Λογαριασμοί", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status))
            .ToHaveTextAsync("Δεν βρέθηκαν λογαριασμοί.", new() { Timeout = 30_000 });
    }
}
