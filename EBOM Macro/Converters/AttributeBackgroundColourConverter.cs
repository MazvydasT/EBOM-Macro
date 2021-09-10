using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace EBOM_Macro.Converters
{
    public class AttributeBackgroundColourConverter : MarkupExtension, IValueConverter
    {
        private static Brush background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFEEEEEE");

        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value == null ? null : background;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
