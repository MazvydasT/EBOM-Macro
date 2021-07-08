using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

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

        public string Number { get; set; }
        public string Name { get; set; }
        public double Version { get; set; }

        //public Matrix3D LocalTransformation { get; set; }
        public Vector3D Rotation { get; set; }
        public Vector3D Translation { get; set; }

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public string Owner { get; set; }

        public string PhysicalId { get; set; }

        public EBOMReportRecord.MaturityState? Maturity { get; set; }

        public Item Parent { get; set; }
        public List<Item> Children { get; } = new List<Item>();
        public IReadOnlyCollection<Item> RedundantChildren { get; set; }

        public string BaseExternalId { get; set; }

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
