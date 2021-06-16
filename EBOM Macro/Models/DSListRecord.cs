namespace EBOM_Macro.Models
{
    public struct DSListRecord
    {
        public string Number { get; set; }

        public double Version { get; set; }

        public string Name { get; set; }

        public int Level { get; set; }

        public string ExternalId { get; set; }

        public byte[] Hash { get; set; }
    }
}
