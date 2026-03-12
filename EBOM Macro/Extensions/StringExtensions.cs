using System.IO;
using System.Linq;
using System.Text;

namespace EBOM_Macro.Extensions
{
    public static class StringExtensions
    {
        static Encoding UTF8_ENCODING = Encoding.UTF8;
        public static string GetSafeFileName(this string value) => string.Concat(
            value.Split(Path.GetInvalidFileNameChars())
                .SelectMany(s => s.Where(c => UTF8_ENCODING.GetBytes(c.ToString()).Length == 1))
        );
    }
}
