using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using WebBanking.Client.Services;
using WebBanking.Client.Services.Interfaces;
using WebBanking.Contracts.Auth;

namespace WebBanking.Client.Pages;

public partial class Login
{
    [Inject]
    private IApiClient ApiClient { get; set; } = default!;

    [Inject]
    private JwtAuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private readonly LoginFormModel model = new();
    private EditContext editContext = default!;
    private ValidationMessageStore messageStore = default!;
    private string? errorMessage;
    private bool isSubmitting;

    protected override void OnInitialized()
    {
        editContext = new EditContext(model);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnFieldChanged += (_, e) =>
        {
            messageStore.Clear(e.FieldIdentifier);
            editContext.NotifyValidationStateChanged();
        };
    }

    private async Task HandleLoginAsync()
    {
        errorMessage = null;
        messageStore.Clear();

        var request = new LoginRequest
        {
            Username = model.Username,
            Password = model.Password
        };

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);

        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            foreach (var result in validationResults)
            {
                foreach (var memberName in result.MemberNames)
                {
                    messageStore.Add(editContext.Field(memberName), result.ErrorMessage ?? "Μη έγκυρη τιμή.");
                }
            }

            editContext.NotifyValidationStateChanged();
            return;
        }

        editContext.NotifyValidationStateChanged();
        isSubmitting = true;

        try
        {
            var result = await ApiClient.LoginAsync(request);

            if (!result.IsSuccess)
            {
                errorMessage = result.Error?.Message
                    ?? "Λάθος όνομα χρήστη ή κωδικός πρόσβασης.";
                model.Password = string.Empty;
                return;
            }

            AuthStateProvider.NotifyAuthStateChanged();
            Navigation.NavigateTo("/");
        }
        catch (Exception)
        {
            errorMessage = "Παρουσιάστηκε σφάλμα κατά τη σύνδεση. Παρακαλούμε δοκιμάστε ξανά.";
        }
        finally
        {
            isSubmitting = false;
        }
    }

    public sealed class LoginFormModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
