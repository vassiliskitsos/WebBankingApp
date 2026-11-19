namespace WebBanking.Api.Data.Entities;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    // Hash only. Never store plaintext passwords.
    public string PasswordHash { get; set; } = string.Empty;

    // See README section 4.
    public Role Role { get; set; } = Role.Customer;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
