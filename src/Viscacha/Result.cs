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

    public T Unwrap()
    {
        return this switch
        {
            Ok ok => ok.Value,
            _ => throw new InvalidOperationException("Unwrapping an error result"),
        };
    }

    public E UnwrapError()
    {
        return this switch
        {
            Err err => err.Error,
            _ => throw new InvalidOperationException("Unwrapping a success result"),
        };
    }
}

public abstract record Result<E>(bool IsSuccess)
{
    public sealed record Ok() : Result<E>(true);
    public sealed record Err(E Error) : Result<E>(false);

    public static implicit operator Result<E>(E err) => new Err(err);

    public Result<U, E> Map<U>(Func<U> map) => this switch
    {
        Ok _ => new Result<U, E>.Ok(map()),
        Err err => new Result<U, E>.Err(err.Error),
        _ => throw new InvalidOperationException("Invalid result type") // Unreachable
    };

    public Result<U, E> Then<U>(Func<Result<U, E>> map) => this switch
    {
        Ok _ => map(),
        Err err => new Result<U, E>.Err(err.Error),
        _ => throw new InvalidOperationException("Invalid result type") // Unreachable
    };

    public Result<U> Else<U>(Func<E, Result<U>> map) => this switch
    {
        Ok _ => new Result<U>.Ok(),
        Err err => map(err.Error),
        _ => throw new InvalidOperationException("Invalid result type") // Unreachable
    };

    public E UnwrapError()
    {
        return this switch
        {
            Err err => err.Error,
            _ => throw new InvalidOperationException("Unwrapping a success result"),
        };
    }
}