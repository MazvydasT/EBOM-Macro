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
    public class StrikethroughAngleConverter : MarkupExtension, IValueConverter
    {
        double min, max;

        public StrikethroughAngleConverter(double min, double max)
        {
            this.min = min;
            this.max = max;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => StaticResources.Random.NextDouble(min, max);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
