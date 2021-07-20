using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Media3D;

namespace EBOM_Macro.Converters
{
    public class ItemToolTipConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == null) return null;

            var state = (Item.ItemState)values[0];

            var changedAttributes = (Dictionary<string, (string, string)>)values[1];

            var tooltip = "";

            if (state == Item.ItemState.Modified && (changedAttributes?.Count ?? 0) > 0)
            {
                return "Changed attributes:\n" + string.Join("\n", (changedAttributes)?.Select(p => $"  {p.Key}: {p.Value.Item1} → {p.Value.Item2}") ?? Enumerable.Empty<string>());
            }

            else tooltip = "Children added / removed";
            
            switch (state)
            {
                case Item.ItemState.New: tooltip = "New"; break;
                case Item.ItemState.Redundant: tooltip = "Removed"; break;
                case Item.ItemState.Unchanged: tooltip = "Unchanged"; break;
                case Item.ItemState.HasModifiedDescendants: tooltip = "Has modified descendents"; break;
                default: break;
            }

            var zeroVectorValue = $"{new Vector3D()}";

            var attributes = ((Dictionary<string, string>)values[2])
                ?.Where(p => p.Key != nameof(ItemAttributes.Name) && p.Key != nameof(ItemAttributes.Number) && p.Key != nameof(ItemAttributes.Version) && !string.IsNullOrWhiteSpace(p.Value) && p.Value != zeroVectorValue)
                .Select(p => $"  {p.Key}: {p.Value}") ?? Enumerable.Empty<string>();

            return ($"{tooltip}\n" + string.Join("\n", attributes)).Trim();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
