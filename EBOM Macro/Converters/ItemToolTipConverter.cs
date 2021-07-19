using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class ItemToolTipConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch((Item.ItemState)values[0])
            {
                case Item.ItemState.New: return $"New";
                case Item.ItemState.Redundant: return "Removed";
                case Item.ItemState.Unchanged: return "Unchanged";
                case Item.ItemState.HasModifiedDescendants: return "Has modified descendents";
                case Item.ItemState.Modified:
                    var changedAttributes = (Dictionary<string, (string, string)>)values[1];

                    return (changedAttributes?.Count ?? 0) > 0 ?
                        ("Changed attributes:" + string.Join("\n", (changedAttributes)?.Select(p => $"{p.Key}: {p.Value.Item1} → {p.Value.Item2}") ?? Enumerable.Empty<string>())) :
                        $"Children added/removed";
                default: return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
