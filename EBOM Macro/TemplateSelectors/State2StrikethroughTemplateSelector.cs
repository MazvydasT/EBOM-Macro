using System.Windows;
using System.Windows.Controls;
using static EBOM_Macro.Models.Item;

namespace EBOM_Macro.TemplateSelectors
{
    public class State2StrikethroughTemplateSelector : DataTemplateSelector
    {
        public DataTemplate BlankTemplate { get; set; }
        public DataTemplate StrikethroughTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // Null value can be passed by IDE designer
            if (item == null) return null;

            return ((ItemState)item) == ItemState.Redundant ? StrikethroughTemplate : BlankTemplate;
        }
    }
}
