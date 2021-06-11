using EBOM_Macro.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class StrikethroughOffsetConverter : MarkupExtension, IValueConverter
    {
        double min, max;

        public StrikethroughOffsetConverter(double min, double max)
        {
            this.min = min;
            this.max = max;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new Thickness(StaticResources.Random.NextDouble(min, max), 0, StaticResources.Random.NextDouble(min, max), 0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
