using System;

namespace EBOM_Macro.Extensions
{
    public static class DoubleExtensions
    {
        public static double Clamp(this double value, double min, double max) => Math.Min(Math.Max(value, min), max);

        public static bool IsInExclusiveRange(this double value, double min, double max) => min < value && value < max;
    }
}
