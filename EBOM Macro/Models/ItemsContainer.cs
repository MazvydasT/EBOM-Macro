using System.Collections.Generic;

namespace EBOM_Macro.Models
{
    public struct ItemsContainer
    {
        public Item Root { get; set; }
        public IReadOnlyCollection<Item> PHs { get; set; }
        public IReadOnlyCollection<Item> Items { get; set; }

        public string Program { get; set; }
    }
}
