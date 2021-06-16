using EBOM_Macro.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class State2ColourConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((Item.ItemState)value)
            {
                case Item.ItemState.New: return StaticResources.GreenBrush.Clone();
                case Item.ItemState.Redundant: return StaticResources.RedBrush.Clone();
                case Item.ItemState.Unchanged: return StaticResources.ClearBrush.Clone();
                case Item.ItemState.Modified: return StaticResources.OrangeBrush.Clone();
                case Item.ItemState.HasModifiedDescendants: return StaticResources.GreyBrush.Clone();
                default: return StaticResources.ClearBrush.Clone();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
