using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static EBOM_Macro.Models.FileCopyMessage;

namespace EBOM_Macro.Managers
{
    public static class FileManager
    {
        private static char[] directorySeparatorCharacters = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public static async Task CopyFiles(IReadOnlyList<FileCopyData> fileCopyDataList, IProgress<FileCopyProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Factory.StartNew(() =>
            {
                var fileCounter = 0;

                foreach (var fileCopyData in fileCopyDataList)
                {
                    foreach (var instance in fileCopyData.Items.Items.Where(i => (i.IsChecked ?? true) && i.IsInstance))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var directoryRelativeFilePath = instance.Attributes.FilePath.Replace(fileCopyData.SystemRootRelativeLDIPath, "");
                        var sourceFilePath = Path.Combine(fileCopyData.SourceLDIPath, directoryRelativeFilePath.Trim(directorySeparatorCharacters));
                        var destinationFilePath = Path.Combine(fileCopyData.DestinationLDIPath, directoryRelativeFilePath.Trim(directorySeparatorCharacters));

                        string message = null;
                        var messageType = MessageType.Information;

                        if (!File.Exists(sourceFilePath))
                        {
                            message = "Source file does not exist";
                            messageType = MessageType.Warning;

                            goto ReportProgress;
                        }

                        var destinationFileExists = File.Exists(destinationFilePath);

                        if (destinationFileExists && !fileCopyData.Overwrite)
                        {
                            message = "Destination file exists - skipping";
                            messageType = MessageType.Information;

                            goto ReportProgress;
                        }

                        if (!destinationFileExists || (destinationFileExists && fileCopyData.Overwrite))
                        {
                            try
                            {
                                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);

                                if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

                                File.Copy(sourceFilePath, destinationFilePath, fileCopyData.Overwrite);

                                message = destinationFileExists ? "Copied overwriting existing file" : "Copied";
                                messageType = MessageType.Information;
                            }

                            catch (Exception exception)
                            {
                                message = exception.Message;
                                messageType = MessageType.Warning;
                            }
                        }

                    ReportProgress:
                        progress?.Report(new FileCopyProgressUpdate
                        {
                            Value = ++fileCounter,
                            FileCopyMessage = new FileCopyMessage
                            {
                                SourceFilePath = sourceFilePath,
                                DestinationFilePath = destinationFilePath,
                                Message = message,
                                Type = messageType
                            }
                        });
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
