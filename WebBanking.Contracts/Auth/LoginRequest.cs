using System.ComponentModel.DataAnnotations;

namespace WebBanking.Contracts.Auth;

public sealed record LoginRequest
{
    [Required(ErrorMessage = "Το όνομα χρήστη είναι υποχρεωτικό.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Ο κωδικός πρόσβασης είναι υποχρεωτικός.")]
    public string Password { get; init; } = string.Empty;
}
