using System.Collections.Generic;


namespace Viscacha.TestRunner.Util;

internal static class EnumerableExtensions
{
    public static IEnumerable<(int Index, T Value)> Enumerate<T>(this IEnumerable<T> source)
    {
        int index = 0;
        foreach (var item in source)
        {
            yield return (index++, item);
        }
    }
}