using EBOM_Macro.Models;
using EBOM_Macro.States;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EBOM_Macro.TemplateSelectors
{
    public class TabHeaderContentSelector : DataTemplateSelector
    {
        public DataTemplate AddTemplate { get; set; }
        public DataTemplate RegularTabTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // Null value can be passed by IDE designer
            if (item == null) return null;

            return AppState.State.Sessions.LastOrDefault() == ((SessionWrapper)item).Session ? AddTemplate : RegularTabTemplate;
        }
    }
}
