using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public static class VectorExtensions
    {
        public static double Item(this Vector3D vector, int index) => index <= 0 ? vector.X : (index == 1 ? vector.Y : vector.Z);
        public static IEnumerable<double> Items(this Vector3D vector)
        {
            yield return vector.X;
            yield return vector.Y;
            yield return vector.Z;
        }
    }
}
