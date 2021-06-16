using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

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

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new TransformGroup
            {
                Children = new TransformCollection(new RotateTransform(StaticResources.Random.NextDouble(min, max)).AsFrozen().Yield()).AsFrozen()
            }.AsFrozen();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
