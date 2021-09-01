﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

        //public string Maturity { get; set; }

        public Item Parent { get; set; }
        public List<Item> Children { get; } = new List<Item>();
        public IReadOnlyCollection<Item> RedundantChildren { get; set; }
        public Item[] AllChildren => Children.Concat(RedundantChildren ?? Enumerable.Empty<Item>())
            .OrderBy(i => i.Attributes.Number)
            .ThenBy(i => i.Attributes.Version)
            .ToArray();

        public IReadOnlyDictionary<string, (string, string)> ChangedAttributes { get; set; }

        public string BaseExternalId { get; set; }
        public string ReusedExternalId { get; set; }

        public ItemType Type { get; set; }
        public ItemState State { get; set; } = ItemState.New;

        public bool IsInstance { get; set; }

        public string Title => $"{Attributes.Number}" + (Attributes.Version == 0 ? "" : $"/{Attributes.Version}") +
            (string.IsNullOrWhiteSpace(Attributes.Name) ? "" : $" - {Attributes.Name}"); //+
            //((Type == ItemType.DS || Type == ItemType.PartAsy) && !string.IsNullOrWhiteSpace(Maturity) ? $" [{Maturity}]" : "");

        bool? isChecked;

        public bool? IsChecked
        {
            get => isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded { get; set; }
    }
}
