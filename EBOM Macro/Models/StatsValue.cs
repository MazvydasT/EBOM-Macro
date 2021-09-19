using PropertyChanged;

namespace EBOM_Macro.Models
{
    [AddINotifyPropertyChangedInterface]
    public class StatsValue
    {
        public string Value { get; set; }
        public int SelectedValue { get; set; }
        public int AvailableValue { get; set; }
        public bool IsColumnHeader { get; set; }
        public bool IsRowHeader { get; set; }
        public int ColumnIndex { get; set; }
        public int RowIndex { get; set; }
    }
}
