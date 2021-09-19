using EBOM_Macro.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class StatsTableBorderConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var statsValue = (StatsValue)value;

            return new Thickness
            {
                Bottom = statsValue.RowIndex == 0 ? 1 : 0,
                Right = statsValue.ColumnIndex == 0 ? 1 : 0
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
