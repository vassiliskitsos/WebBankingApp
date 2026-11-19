using System.ComponentModel.DataAnnotations;

namespace WebBanking.Contracts.Transfers;

public sealed record TransferRequest
{
    [Required]
    public int FromAccountId { get; init; }

    [Required(ErrorMessage = "Το IBAN παραλήπτη είναι υποχρεωτικό.")]
    public string ToIban { get; init; } = string.Empty;

    [Range(0.01, 1_000_000, ErrorMessage = "Το ποσό πρέπει να είναι μεγαλύτερο από 0.")]
    public decimal Amount { get; init; }

    [MaxLength(200)]
    public string Description { get; init; } = string.Empty;
}
