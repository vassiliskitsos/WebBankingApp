namespace WebBanking.Contracts.Accounts;

public sealed record AccountDto(
    int Id,
    string Iban,
    decimal Balance,
    string Currency
);
