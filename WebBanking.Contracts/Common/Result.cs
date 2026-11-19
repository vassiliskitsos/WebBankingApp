namespace WebBanking.Contracts.Common;

public readonly record struct Result<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(Error error) => new(default, error);
}

public readonly record struct Result(Error? Error)
{
    public bool IsSuccess => Error is null;

    public static Result Ok() => new(null);

    public static Result Fail(Error error) => new(error);
}
