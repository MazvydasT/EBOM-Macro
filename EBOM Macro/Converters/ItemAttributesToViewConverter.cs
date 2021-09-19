using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{

    public class ItemAttributesToViewConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public List<AttributeViewItem> Convert(Item item) =>
            (List<AttributeViewItem>)Convert(new object[] { item.ChangedAttributes, item.Attributes.AsDictionary }, null, null, null);

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var attributeList = new List<AttributeViewItem>();

            var changedAttributes = ((IReadOnlyDictionary<string, (string, string)>)values[0]);

            var attributes = (((Dictionary<string, string>)values[1]) ?? new Item().Attributes.AsDictionary);

            if (attributes != null)
            {
                foreach (var pair in attributes)
                {
                    (string, string)? changedValue = null;
                    (string, string) currentAndNew = default;
                    if (changedAttributes?.TryGetValue(pair.Key, out currentAndNew) ?? false) changedValue = currentAndNew;

                    attributeList.Add(new AttributeViewItem
                    {
                        Name = pair.Key,
                        CurrentValue = changedValue.HasValue ? changedValue.Value.Item1 : pair.Value ?? "",
                        NewValue = changedValue.HasValue ? changedValue.Value.Item2 : null
                    });
                }
            }

            return attributeList;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
