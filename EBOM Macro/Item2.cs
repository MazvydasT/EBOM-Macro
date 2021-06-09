﻿using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro
{
    public class Item2
    {
        public enum ItemType
        {
            DS,
            PH,
            PartAsy
        }

        public string Number { get; set; }
        public string Name { get; set; }
        public double Version { get; set; }

        public Matrix3D LocalTransformation { get; set; }

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public string Owner { get; set; }

        public string PhysicalId { get; set; }

        public Item2 Parent { get; set; }
        public List<Item2> Children { get; } = new List<Item2>();

        //public string ExternalId { get; set; }
        //public string Hash { get; set; }

        public ItemType Type { get; set; }
    }
}
