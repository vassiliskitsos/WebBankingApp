using System.ComponentModel.DataAnnotations;

namespace WebBanking.Contracts.Auth;

public sealed record RegisterRequest
{
    [Required(ErrorMessage = "Το όνομα χρήστη είναι υποχρεωτικό.")]
    [MinLength(3, ErrorMessage = "Το όνομα χρήστη πρέπει να έχει τουλάχιστον 3 χαρακτήρες.")]
    [MaxLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Ο κωδικός πρόσβασης είναι υποχρεωτικός.")]
    [MinLength(6, ErrorMessage = "Ο κωδικός πρέπει να έχει τουλάχιστον 6 χαρακτήρες.")]
    public string Password { get; init; } = string.Empty;
}
