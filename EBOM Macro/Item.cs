using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public class Item : INotifyPropertyChanged
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
            //ModifiedWithHierarchy,
            Unchanged,
            //UnchangedWithHierarchy,
            HasModifiedDescendants
        }

        public Item()
        {
            SelectWithoutDescendants = new Command(param => SetIsChecked(true, false, true));
        }

        public Item(string hash) : this()
        {
            this.hash = hash;
        }

        public string ExternalId { get; set; }

        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public double Version { get; set; } = 0;

        public Matrix3D LocalTransformation { get; set; }

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public ItemType Type { get; set; } = ItemType.PartAsy;

        public string CPSC { get; set; }

        public string Owner { get; set; }

        public string PhysicalId { get; set; }

        public IEnumerable<Item> RedundantChildren { get; set; }
        public List<Item> Children { get; } = new List<Item>();
        public IEnumerable<Item> ChildrenOrdered => (RedundantChildren ?? Enumerable.Empty<Item>()).Concat(Children).OrderBy(c => c.Title);

        public Item Parent { get; set; }

        public bool IsExpanded { get; set; }

        private bool? isChecked = true;
        [DoNotNotify]
        public bool? IsChecked
        {
            get => isChecked;
            set { SetIsChecked(value, true, true); }
        }

        public ICommand SelectWithoutDescendants { get; }

        public ItemState State { get; set; }

        public Brush BackgroundColour
        {
            get
            {
                switch(State)
                {
                    case ItemState.New: return StaticResources.GreenBrush;
                    
                    case ItemState.Redundant: return StaticResources.RedBrush;

                    case ItemState.Modified: return StaticResources.OrangeBrush;
                    //case ItemState.ModifiedWithHierarchy: return StaticResources.OrangeBrush;

                    case ItemState.Unchanged: return StaticResources.ClearBrush;
                    //case ItemState.UnchangedWithHierarchy: return StaticResources.ClearBrush;

                    case ItemState.HasModifiedDescendants: return StaticResources.GreyBrush;
                    
                    default: return default;
                }
            }
        }

        public Visibility StrikethroughVisibility => State == ItemState.Redundant ? Visibility.Visible : Visibility.Collapsed;

        public double StrikethroughAngle => StaticResources.Random.NextDouble(-0.65, 0.65);
        public Thickness StrikethroughOffset => new Thickness(StaticResources.Random.NextDouble(-3.0, -0.5), 0, StaticResources.Random.NextDouble(-3.0, -0.5), 0);

        public Visibility CheckBoxVisibility => State == ItemState.Redundant ? Visibility.Hidden : Visibility.Visible;

        private bool dsChecked = false;
        private Item parentDS = null;
        public Item GetDS(bool refreshCache = false)
        {
            if(!dsChecked || refreshCache)
            {
                parentDS = Type == ItemType.DS ? this : Parent?.GetDS(refreshCache) ?? null;
                dsChecked = true;
            }

            return parentDS;
        }

        private Matrix3D? absoluteTransformation = null;
        public Matrix3D GetAbsoluteTransformation(bool refreshCache = false)
        {
            if(absoluteTransformation == null || refreshCache)
            {
                absoluteTransformation = LocalTransformation * Parent?.GetAbsoluteTransformation(refreshCache) ?? Matrix3D.Identity;
            }

            return absoluteTransformation.Value;
        }

        public string Title => $"{Number}" + (Version > 0 ? $"/{Version}" : "") + (string.IsNullOrWhiteSpace(Name) ? "" : $" - {Name}");
        
        public override string ToString() => Title;

        public string CombinedAttributes => $"{Number}-{Version}-{Name}-{GetAbsoluteTransformation()}-{CPSC}-{Prefix}-{Base}-{Suffix}-{Owner}";

        private string hash = null;
        public string GetHash(bool updateCache = false)
        {
            if(hash == null || updateCache)
            {
                var childHashes = Children.Select(c => c.GetHash(updateCache));
                var data = Encoding.UTF8.GetBytes($"{CombinedAttributes}-{PhysicalId}-{string.Join("-", childHashes)}");

                using (var hasher = new SHA512Managed())
                {
                    hash = BitConverter.ToString(hasher.ComputeHash(data)).Replace("-", "");
                }
            }

            return hash;
        }

        public IEnumerable<Item> GetSelfAndDescendants() => Children.SelectMany(c => c.GetSelfAndDescendants()).Prepend(this);

        public IEnumerable<Item> GetDSToSelfPath() => GetDS() == null ? null : (Type == ItemType.DS ? this.Yield() : Parent.GetDSToSelfPath().Append(this));

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == isChecked) return;

            isChecked = value;

            if (updateChildren && isChecked.HasValue) Children.ForEach(c => c.SetIsChecked(isChecked, true, false));

            if (updateParent && Parent != null) Parent.VerifyCheckedState();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }

        void VerifyCheckedState()
        {
            bool? state = null;

            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetIsChecked(state, false, true);
        }
    }
}