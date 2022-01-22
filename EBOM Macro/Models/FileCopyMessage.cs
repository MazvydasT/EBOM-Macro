using System;

namespace EBOM_Macro.Models
{
    public class FileCopyMessage
    {
        public enum MessageType
        {
            Information,
            Warning
        }

        public DateTime Timestamp { get; private set; }
        public MessageType Type { get; set; }
        public string Message { get; set; }
        public string SourceFilePath { get; set; }
        public string DestinationFilePath { get; set; }

        public FileCopyMessage()
        {
            Timestamp = DateTime.Now;
        }
    }
}
