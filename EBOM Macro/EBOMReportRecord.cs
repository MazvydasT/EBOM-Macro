
using CsvHelper.Configuration.Attributes;

namespace EBOM_Macro
{
    public class EBOMReportRecord
    {
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

        [Name("CPSC")]
        public string CPSC { get; set; }

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
    }
}
