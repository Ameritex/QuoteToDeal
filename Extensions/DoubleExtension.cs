using ServiceStack;
using System.Collections.Generic;

namespace Quote_To_Deal.Extensions
{
    public static class DoubleExtension
    {
        public static bool IsBetween<T>(this T item, T start, T end)
        {
            return Comparer<T>.Default.Compare(item, start) >= 0
                && Comparer<T>.Default.Compare(item, end) <= 0;
        }
    }
}
