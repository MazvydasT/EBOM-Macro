using PropertyChanged;

namespace EBOM_Macro.Models
{
    [AddINotifyPropertyChangedInterface]
    public class TooltipDataContainer
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
