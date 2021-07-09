using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EBOM_Macro.Extensions
{
    public static class StringExtensions
    {
        public static string GetSafeFileName(this string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars()));
    }
}
