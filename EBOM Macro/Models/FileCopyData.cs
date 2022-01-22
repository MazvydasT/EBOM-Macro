namespace EBOM_Macro.Models
{
    public struct FileCopyData
    {
        public ItemsContainer Items { get; set; }
        public string SourceLDIPath { get; set; }
        public string DestinationLDIPath { get; set; }
        public string SystemRootRelativeLDIPath { get; set; }
        public bool Overwrite { get; set; }
    }
}
