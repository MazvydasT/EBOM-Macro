using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class ItemChildrenConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            ((IEnumerable<Item>)values[0]).Concat((IEnumerable<Item>)values[1] ?? Enumerable.Empty<Item>())
            .OrderBy(i => i.Number).ThenBy(i => i.Version);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
