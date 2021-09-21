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

                if(parameter != null)
                {
                    var inputState = (InputState)parameter;
                    ItemManager.UpdateStats(inputState.Items, inputState.StatsState);
                }
            });

            ResetSelection = new Command(parameter =>
            {
                ItemManager.ResetItemSelection(this);

                var inputState = (InputState)parameter;
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

        private ConditionalWeakTable<object, object> cache = new ConditionalWeakTable<object, object>();

        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public Item GetDS(object cacheKey = null)
        {
            if (Type == ItemType.DS) return this;
            if (Type == ItemType.PH) return null;

            if (cacheKey != null)
            {
                if (cache.TryGetValue(cacheKey, out var cachedData)) return (Item)cachedData;
                else
                {
                    var data = Parent?.GetDS(cacheKey);
                    cache.Add(cacheKey, data);
                    return data;
                }
            }

            return Parent?.GetDS(cacheKey);
        }

        public IEnumerable<Item> GetDSToSelfPath()
        {
            if (Type == ItemType.DS) return this.Yield();
            if (Type == ItemType.PH || GetDS() == null) return Enumerable.Empty<Item>();

            return (Parent?.GetDSToSelfPath() ?? Enumerable.Empty<Item>()).Append(this);
        }

        public IEnumerable<Item> GetSelfAndDescendants(object cacheKey = null)
        {
            if (cacheKey != null)
            {
                if (cache.TryGetValue(cacheKey, out var cachedData)) return (IList<Item>)cachedData;
                else
                {
                    var data = Children.SelectMany(c => c.GetSelfAndDescendants(cacheKey)).Prepend(this).ToList();
                    cache.Add(cacheKey, data);
                    return data;
                }
            }

            return Children.SelectMany(c => c.GetSelfAndDescendants(cacheKey)).Prepend(this);
        }


        public IEnumerable<Item> GetAncestors(object cacheKey = null)
        {
            if (cacheKey != null)
            {
                if (cache.TryGetValue(cacheKey, out var cachedData)) return (IList<Item>)cachedData;
                else
                {
                    var data = (Parent == null ? Enumerable.Empty<Item>() : Parent.GetAncestors(cacheKey).Append(Parent)).ToList();
                    cache.Add(cacheKey, data);
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