using PropertyChanged;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EBOM_Macro.Models
{
    [AddINotifyPropertyChangedInterface]
    public class ItemAttributes
    {
        public string Number { get; set; }
        public double Version { get; set; }
        public string Name { get; set; }

        public Vector3D Translation { get; set; }

        public double TranslationX => Translation.X;
        public double TranslationY => Translation.Y;
        public double TranslationZ => Translation.Z;

        public Vector3D Rotation { get; set; }

        public double RotationX => Rotation.X;
        public double RotationY => Rotation.Y;
        public double RotationZ => Rotation.Z;

        public string Prefix { get; set; }
        public string Base { get; set; }
        public string Suffix { get; set; }

        public string Owner { get; set; }

        public string FilePath { get; set; }

        public string Material { get; set; }

        public string CCR { get; set; }

        public Dictionary<string, string> AsDictionary =>
            new Dictionary<string, string>
            {
                { nameof(Number), Number },
                { nameof(Version), Version == 0 ? "" : Version.ToString() },
                { nameof(Name), Name },

                { "Translation X", TranslationX == 0 ? "" : TranslationX.ToString() },
                { "Translation Y", TranslationY == 0 ? "" : TranslationY.ToString() },
                { "Translation Z", TranslationZ == 0 ? "" : TranslationZ.ToString() },

                { "Rotation X", RotationX == 0 ? "" : RotationX.ToString() },
                { "Rotation Y", RotationY == 0 ? "" : RotationY.ToString() },
                { "Rotation Z", RotationZ == 0 ? "" : RotationZ.ToString() },

                { nameof(Prefix), Prefix },
                { nameof(Base), Base },
                { nameof(Suffix), Suffix },

                { nameof(Owner), Owner },

                { "File path", FilePath },

                { nameof(Material), Material },

                { nameof(CCR), CCR }
            };
    }
}