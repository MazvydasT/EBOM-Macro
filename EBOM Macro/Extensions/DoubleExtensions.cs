using System;

namespace EBOM_Macro
{
    public static class DoubleExtensions
    {
        public static double Clamp(this double value, double min, double max) => Math.Min(Math.Max(value, min), max);
    }
}
