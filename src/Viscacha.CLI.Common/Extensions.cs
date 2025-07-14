using System;
using System.Diagnostics.CodeAnalysis;
using Viscacha.Model;

namespace Viscacha.CLI.Common;


public static class Extensions
{
    [DoesNotReturn]
    private static object HandleError(Error error)
    {
        Console.Error.WriteLine($"Error: {error.Message}");
        Environment.Exit(-1);
        return null!;
    }

    public static T UnwrapOrExit<T>(this Result<T, Error> result) => result switch
    {
        Result<T, Error>.Ok { Value: T value } => value,
        Result<T, Error>.Err { Error: Error err } => (T)HandleError(err),
        _ => throw new InvalidOperationException("Unexpected result type"),
    };

}
