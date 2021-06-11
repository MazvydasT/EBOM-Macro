using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EBOM_Macro.Extensions
{
    public static class FreezableExtensions
    {
        public static T AsFrozen<T>(this T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();

            return freezable;
        }
    }
}
