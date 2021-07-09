using System.IO;

namespace EBOM_Macro.Extensions
{
    public static class StringExtensions
    {
        public static string GetSafeFileName(this string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars()));
    }
}
