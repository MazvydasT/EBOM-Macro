
using CsvHelper.Configuration.Attributes;

namespace EBOM_Macro
{
    public struct EBOMReportRecord
    {
        public enum MaturityState
        {
            IN_WORK,
            FROZEN
        }

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

        public string CPSCLevel1 => $"{CPSC.Substring(0, 2).PadRight(6, '0')} - ";
        public string CPSCLevel2 => $"{CPSC.Substring(0, 4).PadRight(6, '0')} - ";
        public string CPSCLevel3 => $"{CPSC} - ";

        public MaturityState Maturity => CPSC == "013501" ? MaturityState.IN_WORK : MaturityState.FROZEN;
    }
}
