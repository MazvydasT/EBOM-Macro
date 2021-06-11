using System;
using System.Windows.Media.Media3D;

namespace EBOM_Macro.Extensions
{
    public static class MatrixExtensions
    {
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
    }
}
