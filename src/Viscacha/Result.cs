using System;

namespace Viscacha;

public abstract record Result<T, E>(bool IsSuccess)
{
    public sealed record Ok(T Value) : Result<T, E>(true);
    public sealed record Err(E Error) : Result<T, E>(false);

    public static implicit operator Result<T, E>(T ok) => new Ok(ok);
    public static implicit operator Result<T, E>(E err) => new Err(err);

    public Result<U, E> Map<U>(Func<T, U> map) => this switch
    {
        Ok ok => new Result<U, E>.Ok(map(ok.Value)),
        Err err => new Result<U, E>.Err(err.Error),
        _ => throw new InvalidOperationException("Invalid result type") // Unreachable
    };

    public Result<U, E> Then<U>(Func<T, Result<U, E>> map) => this switch
    {
        Ok ok => map(ok.Value),
        Err err => new Result<U, E>.Err(err.Error),
        _ => throw new InvalidOperationException("Invalid result type") // Unreachable
    };
}
