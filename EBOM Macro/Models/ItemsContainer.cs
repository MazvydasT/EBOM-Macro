using System.Collections.Generic;

namespace EBOM_Macro.Models
{
    public struct ItemsContainer
    {
        public Item Root { get; }
        public IReadOnlyCollection<Item> PHs { get; }
        public IReadOnlyCollection<Item> Items { get; }

        public string Program { get; }

        public object CacheKey { get; private set; }

        public object RefreshCacheKey() => CacheKey = new object();

        public ItemsContainer(Item root, IReadOnlyCollection<Item> phs, IReadOnlyCollection<Item> items, string program, object cacheKey = null)
        {
            Root = root;
            PHs = phs;
            Items = items;

            Program = program;

            CacheKey = cacheKey ?? new object();
        }
    }
}
