using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public static class Extensions
    {
        public static double Clamp(this double value, double min, double max) => Math.Min(Math.Max(value, min), max);

        
        // https://github.com/mrdoob/three.js/blob/master/src/math/Euler.js#L104
        public static Vector3D GetEulerZYX(this Matrix3D matrix)
        {
            var m31 = matrix.M31;

            double x, z;
            var y = Math.Asin(-m31.Clamp(-1.0, 1.0));

            if (Math.Abs(m31) < 0.9999999)
            {
                x = Math.Atan2(matrix.M32, matrix.M33);
                z = Math.Atan2(matrix.M21, matrix.M11);
            }

            else
            {
                x = 0;
                z = Math.Atan2(-matrix.M12, matrix.M22);
            }

            return new Vector3D(x, y, z);
        }

        public static Vector3D GetTranslation(this Matrix3D matrix) => new Vector3D(matrix.M14, matrix.M24, matrix.M34);

        public static double Item(this Vector3D vector, int index) => index <= 0 ? vector.X : (index == 1 ? vector.Y : vector.Z);
        public static IEnumerable<double> Items(this Vector3D vector)
        {
            yield return vector.X;
            yield return vector.Y;
            yield return vector.Z;
        }

        public static IEnumerable<T> Yield<T>(this T item)
        {
            yield return item;
        }
    }
}
