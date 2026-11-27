using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using WebBanking.Client.Services.Interfaces;
using WebBanking.Contracts.Auth;

namespace WebBanking.Client.Pages;

public partial class Register
{
    [Inject]
    private IApiClient ApiClient { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private RegisterFormModel model = new();
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private string? errorMessage;
    private string? successMessage;
    private bool isSubmitting;

    protected override void OnInitialized() => ResetForm();

    private void ResetForm()
    {
        model = new RegisterFormModel();
        editContext = new EditContext(model);
        messageStore = new ValidationMessageStore(editContext);

        // Clear a rule error for a field as soon as the user edits that field.
        editContext.OnFieldChanged += (_, args) => messageStore.Clear(args.FieldIdentifier);
    }

    private async Task HandleSubmitAsync(EditContext context)
    {
        errorMessage = null;
        successMessage = null;
        messageStore.Clear();

        // Checks the confirmation field (only the form knows about it).
        if (!context.Validate())
        {
            return;
        }

        // Username and password rules come from the shared RegisterRequest DTO,
        // so the form and the API use the same rules and messages.
        var request = new RegisterRequest
        {
            Username = model.Username,
            Password = model.Password
        };

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            foreach (var result in results)
            {
                foreach (var member in result.MemberNames)
                {
                    messageStore.Add(new FieldIdentifier(model, member), result.ErrorMessage ?? string.Empty);
                }
            }

            context.NotifyValidationStateChanged();
            return;
        }

        await RegisterAsync(request);
    }

    private async Task RegisterAsync(RegisterRequest request)
    {
        isSubmitting = true;

        try
        {
            var result = await ApiClient.PostAsync("api/auth/register", request, requiresAuth: false);

            if (!result.IsSuccess)
            {
                errorMessage = result.Error?.Message
                    ?? "Παρουσιάστηκε σφάλμα κατά την εγγραφή. Παρακαλούμε δοκιμάστε ξανά.";
                return;
            }

            successMessage = $"Ο λογαριασμός για τον χρήστη '{request.Username}' δημιουργήθηκε επιτυχώς!";
            ResetForm();
        }
        catch (Exception)
        {
            errorMessage = "Παρουσιάστηκε σφάλμα κατά την εγγραφή. Παρακαλούμε δοκιμάστε ξανά.";
        }
        finally
        {
            isSubmitting = false;
        }
    }

    public sealed class RegisterFormModel
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Η επιβεβαίωση κωδικού είναι υποχρεωτική.")]
        [Compare(nameof(Password), ErrorMessage = "Οι κωδικοί πρόσβασης δεν ταιριάζουν.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
