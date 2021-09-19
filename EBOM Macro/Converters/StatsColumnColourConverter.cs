using EBOM_Macro.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class StatsColumnColourConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var statsValue = (StatsValue)((object[])value)[0];

            switch (statsValue.ColumnIndex)
            {
                case 2: return StaticResources.OrangeBrush;
                case 3: return StaticResources.GreenBrush;
                case 4: return StaticResources.RedBrush;
            }

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
