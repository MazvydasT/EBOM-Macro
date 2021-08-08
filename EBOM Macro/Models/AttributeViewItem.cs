using PropertyChanged;

namespace EBOM_Macro.Models
{
    [AddINotifyPropertyChangedInterface]
    public class AttributeViewItem
    {
        public string Name { get; set; }
        public string CurrentValue { get; set; }
        public string NewValue { get; set; }
    }
}
