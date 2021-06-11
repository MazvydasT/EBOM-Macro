using System.Collections.Generic;

namespace EBOM_Macro.Extensions
{
    public static class GenericExtensions
    {
        public static IEnumerable<T> Yield<T>(this T item) { yield return item; }
    }
}