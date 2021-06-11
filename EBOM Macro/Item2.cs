using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public partial class Item2 : ReactiveObject
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

        public Matrix3D LocalTransformation { get; set; }

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public string Owner { get; set; }

        public string PhysicalId { get; set; }

        public Item2 Parent { get; set; }
        public List<Item2> Children { get; } = new List<Item2>();
        public IReadOnlyCollection<Item2> RedundantChildren { get; set; }

        public string BaseExternalId { get; set; }
        //public string ExternalId { get; set; }
        //public string Hash { get; set; }

        public ItemType Type { get; set; }
        [Reactive] public ItemState State { get; set; } = ItemState.New;

        bool? isChecked;
        public bool? IsChecked {
            get => isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded { get; set; }
    }
}
