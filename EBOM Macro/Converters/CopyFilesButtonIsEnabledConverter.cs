using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class CopyFilesButtonIsEnabledConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (!values.Any(v => v == DependencyProperty.UnsetValue)) && (bool)values[0] && ((int)values[1]) > 0;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
