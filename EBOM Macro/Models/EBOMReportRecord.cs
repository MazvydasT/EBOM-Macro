using CsvHelper.Configuration.Attributes;

namespace EBOM_Macro.Models
{
    public class EBOMReportRecord
    {
        [Name("MP")]
        public string MP { get; set; }

        [Name("LEVEL")]
        public int Level { get; set; }

        [Name("VERSION")]
        public double Version { get; set; }

        [Name("PREFIX")]
        public string Prefix { get; set; }

        [Name("BASE")]
        public string Base { get; set; }

        [Name("SUFFIX")]
        public string Suffix { get; set; }

        [Name("NAME")]
        public string Name { get; set; }

        [Name("TRANSFORMATION")]
        public string Transformation { get; set; }

        [Name("OWNER")]
        public string Owner { get; set; }

        [Name("PHYSICAL_ID")]
        public string PhysicalId { get; set; }

        [Name("PART_NUMBER")]
        public string PartNumber { get; set; }

        [Name("CPSC_LEVEL1")]
        public string CPSCLevel1 { get; set; }

        [Name("CPSC_LEVEL2")]
        public string CPSCLevel2 { get; set; }

        [Name("CPSC_LEVEL3")]
        public string CPSCLevel3 { get; set; }

        [Name("VL_TITLE")]
        public string Title { get; set; }

        [Name("VL_NAME")]
        public string VehicleLineName { get; set; }

        [Name("MATERIAL")]
        public string Material { get; set; }

        [Name("HAS_3D_SHAPE")]
        public bool Has3DShape { get; set; }

        [Name("CCR"), Optional]
        public string CCR { get; set; }

        [Name("FILENAME"), Optional]
        public string Filename { get; set; }
    }
}
