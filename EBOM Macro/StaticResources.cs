using EBOM_Macro.Extensions;
using System;
using System.Security.Cryptography;
using System.Windows.Media;

namespace EBOM_Macro
{
    public static class StaticResources
    {
        public static Brush GreyBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3B3B3")).AsFrozen();

        public static Brush OrangeBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF10C")).AsFrozen();

        public static Brush GreenBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52EA46")).AsFrozen();

        public static Brush RedBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FB4040")).AsFrozen();

        public static Brush ClearBrush { get; } = new SolidColorBrush().AsFrozen();

        public static Random Random { get; } = new Random();

        public static SHA256Managed SHA256 { get; } = new SHA256Managed();


    }
}