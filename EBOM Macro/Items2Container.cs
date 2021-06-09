using System.Collections.Generic;

namespace EBOM_Macro
{
    public struct Items2Container
    {
        public Item2 Root { get; set; }
        public IReadOnlyCollection<Item2> PHs { get; set; }
        public IReadOnlyCollection<Item2> Items { get; set; }
    }
}
