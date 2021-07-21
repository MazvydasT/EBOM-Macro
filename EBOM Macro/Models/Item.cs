using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.Generic;

namespace EBOM_Macro.Models
{
    public partial class Item : ReactiveObject
    {
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
        [Reactive] public IReadOnlyCollection<Item> RedundantChildren { get; set; }

        [Reactive] public IReadOnlyDictionary<string, (string, string)> ChangedAttributes { get; set; }

        public string BaseExternalId { get; set; }
        public string ReusedExternalId { get; set; }

        public ItemType Type { get; set; }
        [Reactive] public ItemState State { get; set; } = ItemState.New;

        bool? isChecked;
        public bool? IsChecked
        {
            get => isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded { get; set; }
    }
}
