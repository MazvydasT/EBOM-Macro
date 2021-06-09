using System.Collections.Generic;

namespace EBOM_Macro
{
    public static class GenericExtensions
    {
        public static IEnumerable<T> Yield<T>(this T item) { yield return item; }
    }
}