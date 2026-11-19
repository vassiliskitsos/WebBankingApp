namespace WebBanking.Contracts.Transfers;

public sealed record TransactionDto(
    int Id,
    int FromAccountId,
    string FromIban,
    int ToAccountId,
    string ToIban,
    decimal Amount,
    DateTime CreatedAt,
    string Description
);
