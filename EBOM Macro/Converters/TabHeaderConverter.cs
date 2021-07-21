using EBOM_Macro.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class TabHeaderConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var externalIdPrefix = (string)values[1];

            return (((ItemsContainer?)values[0])?.Program ?? "New tab") + (string.IsNullOrWhiteSpace(externalIdPrefix) ? "" : $" ({externalIdPrefix})");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
