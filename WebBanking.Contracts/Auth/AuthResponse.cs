namespace WebBanking.Contracts.Auth;

public sealed record AuthResponse(
    int UserId,
    string Username,
    string Role,
    string Token,
    DateTime ExpiresAtUtc
);
