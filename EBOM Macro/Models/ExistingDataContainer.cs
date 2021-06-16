using System.Collections.Generic;

namespace EBOM_Macro.Models
{
    public struct ExistingDataContainer
    {
        public IReadOnlyDictionary<string, Item> Items { get; set; }
        public string ExternalIdPrefix { get; set; }
    }
}
