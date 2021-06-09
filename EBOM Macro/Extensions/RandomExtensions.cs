using System;

namespace EBOM_Macro
{
    public static class RandomExtensions
    {
        public static double NextDouble(this Random random, double min, double max) => random.NextDouble() * (max - min) + min;
    }
}
