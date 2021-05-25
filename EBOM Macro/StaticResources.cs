using System.Windows.Media;

namespace EBOM_Macro
{
    static class StaticResources
    {
        public static Brush GreenBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7259F300"));
        public static Brush RedBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99E00000"));
        public static Brush OrangeBrush { get; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFFF8000"));
        public static Brush BlueBrush { get; } = new SolidColorBrush(Colors.Blue);
        public static Brush GreyBrush { get; } = new SolidColorBrush(Colors.Gray);
        public static Brush ClearBrush { get; } = new SolidColorBrush();
    }
}