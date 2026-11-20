namespace WebBanking.Api.Repositories;

public sealed class DuplicateUsernameException
    : Exception
{
    public DuplicateUsernameException(Exception innerException)
        : base("The username already exists.", innerException)
    {
    }
}
