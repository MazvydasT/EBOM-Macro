namespace EBOM_Macro.Extensions
{
    public static class IntExtensions
    {
        public static bool IsInExclusiveRange(this int value, int min, int max) => min < value && value < max;
    }
}
