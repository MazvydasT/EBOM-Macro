using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public static class Utils
    {
        public static Matrix3D V6MatrixString2Matrix3D(string matrixString)
        {
            var stringValues = matrixString.Split(new[] { '|' });

            var valuesCount = stringValues.Length;

            if (valuesCount != 12) throw new ArgumentOutOfRangeException(nameof(matrixString), $"Input {matrixString} must contain 12 '|' separated column-major numeric values.");

            var matrix = Matrix3D.Identity;

            for (var i = 0; i < valuesCount; ++i)
            {
                var stringValue = stringValues[i];
                if (double.TryParse(stringValue, out double doubleValue))
                {
                    switch(i)
                    {
                        case 0:
                            matrix.M11 = doubleValue;
                            break;

                        case 1:
                            matrix.M21 = doubleValue;
                            break;

                        case 2:
                            matrix.M31 = doubleValue;
                            break;

                        case 3:
                            matrix.M12 = doubleValue;
                            break;

                        case 4:
                            matrix.M22 = doubleValue;
                            break;

                        case 5:
                            matrix.M32 = doubleValue;
                            break;

                        case 6:
                            matrix.M13 = doubleValue;
                            break;

                        case 7:
                            matrix.M23 = doubleValue;
                            break;

                        case 8:
                            matrix.M33 = doubleValue;
                            break;

                        case 9:
                            matrix.M14 = doubleValue;
                            break;

                        case 10:
                            matrix.M24 = doubleValue;
                            break;

                        case 11:
                            matrix.M34 = doubleValue;
                            break;
                    }
                }

                else
                {
                    throw new ArgumentException($"Value '{stringValue}' at index {i} of input '{matrixString}' is not numeric.", nameof(matrixString));
                }
            }
            
            return matrix;
        }

        private static readonly Regex driveLetterRegexp = new Regex(@"^([A-Za-z]:).*$", RegexOptions.Compiled);
        public static string PathToUNC(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\") || !Path.IsPathRooted(path)) return path;

            path = Path.GetFullPath(path);

            var driveLetterMatch = driveLetterRegexp.Match(path);

            if (driveLetterMatch.Success)
            {
                var drivePath = driveLetterMatch.Groups[1].Value;
                var driveAddress = GetDriveAddress(drivePath);

                if (driveAddress != null)
                {
                    path = path.Replace(drivePath, driveAddress);
                }
            }

            return path;
        }

        private static string GetDriveAddress(string drivePath)
        {
            using (var managementObject = new ManagementObject())
            {
                managementObject.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", (object)drivePath));

                try
                {
                    return Convert.ToUInt32(managementObject["DriveType"]) == 4 ? Convert.ToString(managementObject["ProviderName"]) : drivePath;
                }

                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static IEnumerable<Item> AssignNumberNameAndId(IEnumerable<Item> items, Item root, string program)
        {
            foreach(var item in items)
            {
                if (item != root)
                {
                    item.Number = $"PH-{program}-{item.CPSC}";
                    item.Name = "";
                }

                else
                {
                    item.Number = $"PRG-{program}";
                    item.Name = $"PROGRAM NODE FOR {program}";
                }

                item.ExternalId = $"{item.Number}_c";

                yield return item;
            }
        }
    }
}
