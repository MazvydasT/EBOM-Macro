namespace EBOM_Macro.Models
{
    public struct XMLExportData
    {
        public ItemsContainer Items { get; set; }
        public string ExternalIdPrefix { get; set; }
        public string LDIFolderPath { get; set; }
    }
}
