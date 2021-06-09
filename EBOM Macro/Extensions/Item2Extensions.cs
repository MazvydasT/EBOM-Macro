using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EBOM_Macro
{
    public static class Item2Extensions
    {
        private static readonly ConditionalWeakTable<Item2, Item2> dsLookup = new ConditionalWeakTable<Item2, Item2>();
        public static Item2 GetDS(this Item2 item, bool refreshCache = false)
        {
            if (item.Type == Item2.ItemType.DS) return item;
            if (item.Type == Item2.ItemType.PH) return null;

            if (!refreshCache && dsLookup.TryGetValue(item, out var ds)) return ds;

            ds = item.Parent?.GetDS();

            if (ds != null)
            {
                if (refreshCache) dsLookup.Remove(item);

                dsLookup.Add(item, ds);
            }

            return ds;
        }

        private static readonly ConditionalWeakTable<Item2, IEnumerable<Item2>> dsToItemPathLookup = new ConditionalWeakTable<Item2, IEnumerable<Item2>>();
        public static IEnumerable<Item2> GetDSToItemPath(this Item2 item, bool refreshCache = false)
        {
            if (item.Type == Item2.ItemType.PH || item.GetDS() == null) return Enumerable.Empty<Item2>();
            if (item.Type == Item2.ItemType.DS) return item.Yield();

            if (!refreshCache && dsToItemPathLookup.TryGetValue(item, out var dsToItemPath)) return dsToItemPath;

            dsToItemPath = (item.Parent?.GetDSToItemPath(refreshCache) ?? Enumerable.Empty<Item2>()).Append(item);

            if (refreshCache) dsToItemPathLookup.Remove(item);

            dsToItemPathLookup.Add(item, dsToItemPath);

            return dsToItemPath;
        }

        private static readonly ConditionalWeakTable<Item2, string> externalIdLookup = new ConditionalWeakTable<Item2, string>();
        public static void SetExternaId(this Item2 item, string externalId)
        {
            externalIdLookup.Remove(item);

            if (!string.IsNullOrWhiteSpace(externalId)) externalIdLookup.Add(item, externalId);
        }

        public static string GetExternalId(this Item2 item) => externalIdLookup.TryGetValue(item, out var externalId) ? externalId : null;
    }
}
