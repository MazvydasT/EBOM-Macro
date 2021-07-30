using System.Collections.Generic;
using System.ComponentModel;

namespace EBOM_Macro.Models
{
    public partial class Item : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public enum ItemType
        {
            DS,
            PH,
            PartAsy
        }

        public enum ItemState
        {
            New,
            Redundant,
            Modified,
            Unchanged,
            HasModifiedDescendants
        }

        public ItemAttributes Attributes { get; set; } = new ItemAttributes();

        public string PhysicalId { get; set; }

        public EBOMReportRecord.MaturityState? Maturity { get; set; }

        public Item Parent { get; set; }
        public List<Item> Children { get; } = new List<Item>();
        public IReadOnlyCollection<Item> RedundantChildren { get; set; }

        public IReadOnlyDictionary<string, (string, string)> ChangedAttributes { get; set; }

        public string BaseExternalId { get; set; }
        public string ReusedExternalId { get; set; }

        public ItemType Type { get; set; }
        public ItemState State { get; set; } = ItemState.New;

        public bool IsInstance { get; set; }

        bool? isChecked;

        public bool? IsChecked
        {
            get => isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded { get; set; }
    }
}
