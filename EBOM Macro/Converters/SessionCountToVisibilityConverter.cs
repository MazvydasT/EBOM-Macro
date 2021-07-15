using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class SessionCountToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (int)value < 1 ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
