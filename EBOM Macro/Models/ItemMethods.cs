using EBOM_Macro.Extensions;
using EBOM_Macro.Managers;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace EBOM_Macro.Models
{
    public partial class Item
    {
        public Item()
        {
            SelectWithoutDescendants = new Command(_ => SetIsChecked(true, false, true));
            ResetSelection = new Command(_ => ItemManager.ResetItemSelection(this));
        }

        public ICommand SelectWithoutDescendants { get; }
        public ICommand ResetSelection { get; }

        public Item GetDS()
        {
            if (Type == ItemType.DS) return this;
            if (Type == ItemType.PH) return null;

            return Parent?.GetDS();
        }

        public IEnumerable<Item> GetDSToSelfPath()
        {
            if (Type == ItemType.DS) return this.Yield();
            if (Type == ItemType.PH || GetDS() == null) return Enumerable.Empty<Item>();

            return (Parent?.GetDSToSelfPath() ?? Enumerable.Empty<Item>()).Append(this);
        }

        public IEnumerable<Item> GetSelfAndDescendants() => Children.SelectMany(c => c.GetSelfAndDescendants()).Prepend(this);

        public IEnumerable<Item> GetAncestors() => Parent == null ? Enumerable.Empty<Item>() : Parent.GetAncestors().Prepend(Parent);

        //public string GetAttributesAsString() => $"{Number}-{Version}-{Name}-{Rotation}-{Translation}-{Prefix}-{Base}-{Suffix}";



        public Dictionary<string, (string, string)> GetDifferentAttributes(Item anotherItem)
        {
            if (anotherItem == null) return null;

            var attributes = Attributes.AsDictionary;
            var anotherItemAttributes = anotherItem.Attributes.AsDictionary;

            var differentAttributes = new Dictionary<string, (string, string)>();

            foreach (var pair in attributes)
            {
                var attributeValue = pair.Value ?? "";
                var anotherItemAttributeValue = anotherItemAttributes[pair.Key] ?? "";

                if (attributeValue != anotherItemAttributeValue) differentAttributes[pair.Key] = (attributeValue, anotherItemAttributeValue);
            }

            return differentAttributes;
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == isChecked) return;

            isChecked = value;

            if (updateChildren && isChecked.HasValue) Children.ForEach(c => c.SetIsChecked(isChecked, true, false));

            if (updateParent && Parent != null) Parent.VerifyCheckedState();

            this.RaisePropertyChanged(nameof(IsChecked));
        }

        void VerifyCheckedState()
        {
            bool? state = null;

            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0) state = current;
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetIsChecked(state, false, true);
        }

        public override string ToString() => $"{Attributes.Number}" + (Attributes.Version == 0 ? "" : $"/{Attributes.Version}") +
            (string.IsNullOrWhiteSpace(Attributes.Name) ? "" : $" - {Attributes.Name}") +
            ((Type == ItemType.DS || Type == ItemType.PartAsy) && Maturity.HasValue ? $" [{Maturity}]" : "");
    }
}