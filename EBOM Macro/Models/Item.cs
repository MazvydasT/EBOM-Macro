using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Media3D;

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

        public Item Parent { get; set; }
        public List<Item> Children { get; } = new List<Item>();
        public IReadOnlyCollection<Item> RedundantChildren { get; set; }
        public IReadOnlyList<Item> AllChildren => Children.Concat(RedundantChildren ?? Enumerable.Empty<Item>())
            .OrderBy(i => i.Attributes.Number)
            .ThenBy(i => i.Attributes.Version)
            .ToList().AsReadOnly();

        public IReadOnlyDictionary<string, (string, string)> ChangedAttributes { get; set; }

        public string BaseExternalId { get; set; }
        public string ReusedExternalId { get; set; }

        public ItemType Type { get; set; }
        public ItemState State { get; set; } = ItemState.New;

        public bool IsInstance { get; set; }

        public string Filename { get; set; }

        public string Title => $"{Attributes.Number}" + (Attributes.Version == 0 ? "" : $"/{Attributes.Version}") +
            (string.IsNullOrWhiteSpace(Attributes.Name) ? "" : $" - {Attributes.Name}");

        bool? isChecked;
        public bool? IsChecked
        {
            get => isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded { get; set; }

        public Matrix3D LocalTransformationMatrix { get; set; } = Matrix3D.Identity;

        Matrix3D? absoluteTransformationMatrix;
        public Matrix3D AbsoluteTransformationMatrix
        {
            get
            {
                if (!absoluteTransformationMatrix.HasValue)
                {
                    if (Parent == null)
                        absoluteTransformationMatrix = LocalTransformationMatrix;

                    else
                        absoluteTransformationMatrix =  Parent.AbsoluteTransformationMatrix * LocalTransformationMatrix;
                }

                return absoluteTransformationMatrix.Value;
            }
        }

    }
}
