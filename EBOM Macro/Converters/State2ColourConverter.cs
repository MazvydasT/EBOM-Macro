using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace EBOM_Macro.Converters
{
    public class State2ColourConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch((Item2.ItemState)value)
            {
                case Item2.ItemState.New: return StaticResources.GreenBrush.Clone();
                case Item2.ItemState.Redundant: return StaticResources.RedBrush.Clone();
                case Item2.ItemState.Unchanged: return StaticResources.ClearBrush.Clone();
                case Item2.ItemState.Modified: return StaticResources.OrangeBrush.Clone();
                case Item2.ItemState.HasModifiedDescendants: return StaticResources.GreyBrush.Clone();
                default: return StaticResources.ClearBrush.Clone();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
