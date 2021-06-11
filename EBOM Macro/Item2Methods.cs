using EBOM_Macro.Extensions;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;

namespace EBOM_Macro
{
    public partial class Item2
    {
        public Item2 GetDS()
        {
            if (Type == ItemType.DS) return this;
            if (Type == ItemType.PH) return null;
            
            return Parent?.GetDS();
        }

        public IEnumerable<Item2> GetDSToSelfPath()
        {
            if (Type == ItemType.DS) return this.Yield();
            if (Type == ItemType.PH || GetDS() == null) return Enumerable.Empty<Item2>();
            
            return (Parent?.GetDSToSelfPath() ?? Enumerable.Empty<Item2>()).Append(this);
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

        public override string ToString() => $"{Number}" + (Version == 0 ? "" : $"/{Version}") + (string.IsNullOrWhiteSpace(Name) ? "" : $" - {Name}");
    }
}