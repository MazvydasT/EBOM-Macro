using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    [AddINotifyPropertyChangedInterface]
    public class Item
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
            ModifiedWithHierarchy,
            Unchanged,
            UnchangedWithHierarchy,
            HasModifiedDescendants
        }

        public Item() { }

        public Item(string hash)
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

        public ItemState State { get; set; }

        public Brush BackgroundColour
        {
            get
            {
                switch(State)
                {
                    case ItemState.New: return StaticResources.GreenBrush;
                    
                    case ItemState.Redundant: return StaticResources.RedBrush;
                    
                    case ItemState.ModifiedWithHierarchy: return StaticResources.BlueBrush;

                    case ItemState.Modified: return StaticResources.OrangeBrush;

                    case ItemState.Unchanged: return StaticResources.ClearBrush;
                    case ItemState.UnchangedWithHierarchy: return StaticResources.ClearBrush;

                    case ItemState.HasModifiedDescendants: return StaticResources.GreyBrush;
                    
                    default: return default;
                }
            }
        }

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
    }
}
