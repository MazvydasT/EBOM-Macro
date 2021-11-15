using EBOM_Macro.Extensions;
using EBOM_Macro.Managers;
using EBOM_Macro.States;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EBOM_Macro.Models
{
    public partial class Item
    {
        public Item()
        {
            SelectWithoutDescendants = new Command(parameter =>
            {
                SetIsChecked(true, false, true);

                if (parameter != null)
                {
                    var inputState = (InputState)parameter;
                    ItemManager.UpdateStats(inputState.Items, inputState.StatsState);
                }
            });

            ResetSelection = new Command(parameter =>
            {
                var inputState = (InputState)parameter;

                ItemManager.ResetItemSelection(this, inputState.Items.CacheKey);
                ItemManager.UpdateStats(inputState.Items, inputState.StatsState);
            });

            Click = new Command(parameter =>
            {
                var inputState = (InputState)parameter;
                ItemManager.UpdateStats(inputState.Items, inputState.StatsState);
            });
        }

        public ICommand SelectWithoutDescendants { get; }
        public ICommand ResetSelection { get; }
        public ICommand Click { get; set; }

        private ConditionalWeakTable<object, List<Item>> ancestorsCache = new ConditionalWeakTable<object, List<Item>>();
        private ConditionalWeakTable<object, Item> dsCache = new ConditionalWeakTable<object, Item>();
        private ConditionalWeakTable<object, List<Item>> selfAndDescendantsCache = new ConditionalWeakTable<object, List<Item>>();
        private ConditionalWeakTable<object, List<Item>> selfAndDescendantsWithRedundantCache = new ConditionalWeakTable<object, List<Item>>();

        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public Item GetDS(object cacheKey = null)
        {
            if (Type == ItemType.DS) return this;
            if (Type == ItemType.PH) return null;

            if (cacheKey != null)
            {
                if (dsCache.TryGetValue(cacheKey, out var cachedData)) return cachedData;
                else
                {
                    var data = Parent?.GetDS(cacheKey);
                    dsCache.Add(cacheKey, data);
                    return data;
                }
            }

            return Parent?.GetDS(cacheKey);
        }

        public IEnumerable<Item> GetDSToSelfPath(object cacheKey = null)
        {
            if (Type == ItemType.DS) return this.Yield();
            if (Type == ItemType.PH || GetDS(cacheKey) == null) return Enumerable.Empty<Item>();

            return (Parent?.GetDSToSelfPath(cacheKey) ?? Enumerable.Empty<Item>()).Append(this);
        }

        public IEnumerable<Item> GetSelfAndDescendants(object cacheKey = null, bool includeRedundantItems = false)
        {

            if (!includeRedundantItems)
            {
                if (cacheKey != null && selfAndDescendantsCache.TryGetValue(cacheKey, out var cachedData)) return cachedData;

                else
                {
                    var data = Children.SelectMany(c => c.GetSelfAndDescendants(cacheKey)).Prepend(this);

                    if (cacheKey != null)
                    {
                        var dataList = data.ToList();
                        selfAndDescendantsCache.Add(cacheKey, dataList);
                        data = dataList;
                    }

                    return data;
                }
            }

            else
            {
                if (cacheKey != null && selfAndDescendantsWithRedundantCache.TryGetValue(cacheKey, out var cachedWithRedundantData)) return cachedWithRedundantData;

                else
                {
                    var data = GetSelfAndDescendants(cacheKey).SelectMany(i => (i.RedundantChildren ?? Enumerable.Empty<Item>()).SelectMany(c => c.GetSelfAndDescendants(cacheKey)).Prepend(i));

                    if (cacheKey != null)
                    {
                        var dataList = data.ToList();
                        selfAndDescendantsWithRedundantCache.Add(cacheKey, dataList);
                        data = dataList;
                    }

                    return data;
                }
            }
        }


        public IEnumerable<Item> GetAncestors(object cacheKey = null)
        {
            if (cacheKey != null)
            {
                if (ancestorsCache.TryGetValue(cacheKey, out var cachedData)) return cachedData;
                else
                {
                    var data = (Parent == null ? Enumerable.Empty<Item>() : Parent.GetAncestors(cacheKey).Append(Parent)).ToList();
                    ancestorsCache.Add(cacheKey, data);
                    return data;
                }
            }

            return Parent == null ? Enumerable.Empty<Item>() : Parent.GetAncestors(cacheKey).Append(Parent);
        }

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

                if (!IsInstance || !anotherItem.IsInstance)
                {
                    // Matrial is only applicable to instances in eMS
                    if (pair.Key == nameof(ItemAttributes.Material)) continue;
                }

                if (attributeValue != anotherItemAttributeValue) differentAttributes[pair.Key] = (attributeValue, anotherItemAttributeValue);
            }

            return differentAttributes;
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == isChecked) return;

            isChecked = value;

            if (updateChildren && isChecked.HasValue)
            {
                foreach (var child in Children.Concat(RedundantChildren ?? Enumerable.Empty<Item>()))
                {
                    child.SetIsChecked(isChecked, true, false);
                }
            }

            if (updateParent && Parent != null) Parent.VerifyCheckedState();

            NotifyPropertyChanged(nameof(IsChecked));
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
    }
}