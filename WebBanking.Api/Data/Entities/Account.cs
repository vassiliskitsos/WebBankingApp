using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanking.Api.Data.Entities;

public class Account
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public string Iban { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    // ISO 4217 code, e.g. "EUR".
    public string Currency { get; set; } = "EUR";

    // Optimistic concurrency token. Mapped as IsRowVersion() in AppDbContext (KAN-25).
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
