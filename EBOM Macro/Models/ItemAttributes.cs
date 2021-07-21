using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro.Models
{
    public class ItemAttributes
    {
        public string Number { get; set; }
        public double Version { get; set; }
        public string Name { get; set; }

        public Vector3D Translation { get; set; }
        public Vector3D Rotation { get; set; }

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public string Owner { get; set; }

        public Dictionary<string, string> AsDictionary =>
            new Dictionary<string, string>
            {
                { nameof(Number), Number },
                { nameof(Version), Version.ToString() },
                { nameof(Name), Name },

                { nameof(Translation), Translation.ToString() },
                { nameof(Rotation), Rotation.ToString() },

                { nameof(Prefix), Prefix },
                { nameof(Base), Base },
                { nameof(Suffix), Suffix },

                { nameof(Owner), Owner }
            };

        /*public override bool Equals(object obj)
        {
            if(obj != null && this.GetType().Equals(obj.GetType()))
            {
                var attributes = (ItemAttributes)obj;

                if(Number == attributes.Number && Version == attributes.Version && Name == attributes.Name &&
                    Location == attributes.Location && Rotation == attributes.Rotation &&
                    Prefix == attributes.Prefix && Base == attributes.Base && Suffix == attributes.Suffix &&
                    Owner == attributes.Owner)
                {
                    return true;
                }
            }

            return false;
        }

        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(ItemAttributes lhs, ItemAttributes rhs) => lhs.Equals(rhs);
        public static bool operator !=(ItemAttributes lhs, ItemAttributes rhs) => !(lhs == rhs);*/
    }
}