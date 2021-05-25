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

        //public Item MatchingItem { get; set; }

        public bool IsExpanded { get; set; }

        //private ItemState? state = null;
        public ItemState State { get; set; }
        /*{
            get
            {
                if (!state.HasValue)
                {
                    if ((Parent?.State ?? ItemState.Unchanged) == ItemState.ModifiedWithHierarchy) state = ItemState.ModifiedWithHierarchy;
                    if ((Parent?.State ?? ItemState.Unchanged) == ItemState.UnchangedWithHierarchy) state = ItemState.UnchangedWithHierarchy;

                    if (MatchingItem == null) state = ItemState.New;

                    if (MatchingItem == this) state = ItemState.Redundant;

                    if (Type == ItemType.DS && MatchingItem.Children.Count == 0)
                    {
                        if (GetHash() != MatchingItem.GetHash()) state = ItemState.ModifiedWithHierarchy;
                        else state = ItemState.UnchangedWithHierarchy;
                    }

                    if (Children.Count != MatchingItem.Children.Count) state = ItemState.Modified;

                    var childrenHashSet = Children.Select(i => i.ExternalId).ToHashSet();
                    var matchingChildrenHashSet = MatchingItem.Children.Select(i => i.ExternalId).ToHashSet();

                    if (Children.Where(i => !matchingChildrenHashSet.Contains(i.ExternalId)).Count() > 0 ||
                        MatchingItem.Children.Where(i => !childrenHashSet.Contains(i.ExternalId)).Count() > 0) state = ItemState.Modified;

                    if (CombinedAttributes != MatchingItem.CombinedAttributes) state = ItemState.Modified;

                    state = ItemState.Unchanged;
                }

                return state.Value;
            }
        }*/

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

        //##############################

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

        //#######################

        /*private byte[] selfHashData = null;
        private IEnumerable<byte> GetHashData(bool updateCache = false)
        {
            if(selfHashData == null || updateCache)
            {
                selfHashData = Encoding.UTF8.GetBytes($"{ToString()}{GetAbsoluteTransformation()}{CPSC}");
            }

            return selfHashData.Concat(Children.SelectMany(c => c.GetHashData(updateCache)));
        }*/

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
        
        //public IEnumerable<Item> GetRootToSelfPath() => Parent == null ? this.Yield() : Parent.GetRootToSelfPath().Append(this);

        /*private int level = -1;
        public int GetLevel(bool updateCache = false)
        {
            if(level == -1 || updateCache)
            {
                level = Parent == null ? 0 : Parent.GetLevel(updateCache) + 1;
            }

            return level;
        }*/

        //public int Level => Parent == null ? 0 : Parent.Level + 1;
    }
}
