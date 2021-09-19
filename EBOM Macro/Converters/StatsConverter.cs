using EBOM_Macro.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class StatsConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Any(v => v == DependencyProperty.UnsetValue)) return new object[0][];

            var showSelected = (bool)values[0];
            var showAvailable = (bool)values[1];

            var statValues = (object[][])values[2];

            return statValues.Select((c, ci) => c.Select((v, ri) =>
            {
                string value = null;
                (int selected, int available) numericValues = default;

                if (ci == 0 || ri == 0) value = (string)v;
                else
                {
                    numericValues = ((int, int))v;
                }


                return new StatsValue
                {
                    Value = value,
                    SelectedValue = numericValues.selected,
                    AvailableValue = numericValues.available,
                    IsColumnHeader = ri == 0,
                    IsRowHeader = ci == 0,
                    ColumnIndex = ci,
                    RowIndex = ri
                };
            }).ToArray()).ToArray();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
