namespace WebBanking.Contracts.Common;

public sealed record Error(ErrorType Type, string Message);
