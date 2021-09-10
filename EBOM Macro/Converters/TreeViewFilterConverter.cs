using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class TreeViewFilterConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var children = (IEnumerable<Item>)values[0];

            var includeUnchanged = (bool)values[1];
            var includeModified = (bool)values[2];
            var includeNew = (bool)values[3];
            var includeDeleted = (bool)values[4];

            var cacheKey = new object();

            return children.Where(c =>
            {
                if (includeUnchanged || includeModified || includeNew || includeDeleted)
                    return c.GetSelfAndDescendants(cacheKey).SelectMany(d => (d.RedundantChildren ?? Enumerable.Empty<Item>()).Prepend(d))
                        .Where(d =>
                            (includeUnchanged && d.State == Item.ItemState.Unchanged) ||
                            (includeModified && d.State == Item.ItemState.Modified) ||
                            (includeNew && d.State == Item.ItemState.New) ||
                            (includeDeleted && d.State == Item.ItemState.Redundant)
                        ).Any();

                return false;
            }).ToArray();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
