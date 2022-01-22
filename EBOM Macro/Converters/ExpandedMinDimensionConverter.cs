using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class ExpandedMinDimensionConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public double MinValue { get; set; } = 150;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (bool)value ? MinValue : 0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
