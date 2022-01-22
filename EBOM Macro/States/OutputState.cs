using DynamicData;
using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

namespace EBOM_Macro.States
{
    public class OutputState : ReactiveObject
    {
        [Reactive] public double ExportProgress { get; set; }
        [Reactive] public string ExportMessage { get; set; }
        [Reactive] public bool ExportError { get; set; }

        [Reactive] public int FilesCopied { get; set; }

        [ObservableAsProperty] public bool AllSessionsAreReadyForExport { get; }
        [ObservableAsProperty] public int FilesToCopyCount { get; }

        public ObservableCollection<FileCopyMessage> FileCopyErrors { get; } = new ObservableCollection<FileCopyMessage>();

        public ReactiveCommand<Unit, Unit> SaveXML { get; }
        public ReactiveCommand<Unit, Unit> CancelExport { get; }
        public ReactiveCommand<Unit, Unit> CopyFiles { get; }
        public ReactiveCommand<Unit, Unit> CancelFileCopy { get; }

        IObservable<IReadOnlyCollection<SessionState>> sessionsChangeSetObservable;

        CancellationTokenSource xmlExportCancellationTokenSource, fileCopyCancellationTokenSource;

        public OutputState(IObservable<IChangeSet<SessionState>> sessionsChangeSetObservable)
        {
            this.sessionsChangeSetObservable = sessionsChangeSetObservable.AutoRefresh(s => s.IsReadyForExport)
                .ToCollection().Replay(1).RefCount();

            this.sessionsChangeSetObservable.Select(c => c.Count > 0 && c.All(s => s.IsReadyForExport)).ToPropertyEx(this, x => x.AllSessionsAreReadyForExport);
            this.sessionsChangeSetObservable.Select(
                c => c.Count > 0 ?
                c.Where(s => !string.IsNullOrWhiteSpace(s.InputState.CopyFilesFromPath)).Sum(s => s.InputState.StatsState.SelectedTotalParts) : 0)
                .ToPropertyEx(this, x => x.FilesToCopyCount);

            SaveXML = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedXMLDirectory = string.IsNullOrWhiteSpace(properties.LastUsedXMLDirectory) ? Environment.CurrentDirectory : properties.LastUsedXMLDirectory;

                var dialog = new SaveFileDialog
                {
                    Filter = "eM-Planner data (*.xml)|*.xml",
                    InitialDirectory = lastUsedXMLDirectory,
                    Title = "Save XML",
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog() == true)
                {
                    properties.LastUsedXMLDirectory = Path.GetDirectoryName(dialog.FileName);
                    properties.Save();

                    this.sessionsChangeSetObservable.Take(1).Subscribe(async sessions =>
                    {
                        ExportProgress = 0;
                        ExportMessage = "";
                        ExportError = false;

                        var inputs = sessions.Select(s => s.InputState);
                        var exportDataList = inputs.Select(i => new XMLExportData { Items = i.Items, ExternalIdPrefix = i.ExternalIdPrefix }).ToList();

                        using (xmlExportCancellationTokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                var metaData = string.Join("\n", new[]
                                {
                                    $"                          Timestamp: {DateTime.Now:G}",
                                    ""
                                }.Concat(inputs.SelectMany(i => new[]
                                {
                                    $"                        EBOM report: {i.EBOMReportPath}",
                                    $"Path to LDI is System Root relative: {i.SystemRootRelativePath}",
                                    $"                Path to System Root: {i.SystemRootFolderPath}",
                                    $"                        Path to LDI: {i.LDIFolderPath}",
                                    $"                      Existing data: {i.ExistingDataPath}",
                                    $"                   ExternalId reuse: {(string.IsNullOrWhiteSpace(i.ExistingDataPath) ? false : i.ReuseExternalIds)}",
                                    $"                  ExternalId prefix: {i.ExternalIdPrefix}",
                                    $"   LDI folder to copy JT files from: {i.CopyFilesFromPath}",
                                    $"           Overwrite existing files: {i.OverwriteExistingFiles}",
                                    ""
                                })));

                                await XMLManager.ItemsToXML(dialog.FileName, exportDataList, metaData, new Progress<ProgressUpdate>(progress =>
                                {
                                    ExportProgress = (double)progress.Value / progress.Max;
                                    ExportMessage = progress.Message;
                                }), xmlExportCancellationTokenSource.Token);
                            }

                            catch (OperationCanceledException)
                            {
                                ExportProgress = 0;
                                ExportMessage = "";
                                ExportError = false;
                            }

                            catch (Exception exception)
                            {
                                ExportMessage = exception.Message;
                                ExportError = true;
                            }

                            finally
                            {
                                xmlExportCancellationTokenSource = null;
                            }
                        }
                    });
                }
            });

            CopyFiles = ReactiveCommand.Create(() =>
            {
                FileCopyErrors.Clear();
                FilesCopied = -1;

                this.sessionsChangeSetObservable.Take(1).Subscribe(async sessions =>
                {
                    var fileCopyDataList = sessions.Where(s => s.InputState.StatsState.SelectedTotalParts > 0 && !string.IsNullOrWhiteSpace(s.InputState.CopyFilesFromPath))
                        .Select(s => s.InputState)
                        .Select(i => new FileCopyData
                        {
                            Items = i.Items,
                            SourceLDIPath = i.CopyFilesFromPath,
                            DestinationLDIPath = i.LDIFolderPath,
                            SystemRootRelativeLDIPath = i.AdjustedLDIPath,
                            Overwrite = i.OverwriteExistingFiles
                        }).ToList();

                    using (fileCopyCancellationTokenSource = new CancellationTokenSource())
                    {
                        try
                        {
                            await FileManager.CopyFiles(fileCopyDataList, new Progress<FileCopyProgressUpdate>(fileCopyProgress =>
                            {
                                FileCopyErrors.Insert(0, fileCopyProgress.FileCopyMessage);
                            }), fileCopyCancellationTokenSource.Token);
                        }

                        catch (OperationCanceledException)
                        {
                            FilesCopied = 0;
                        }

                        finally
                        {
                            fileCopyCancellationTokenSource = null;
                        }
                    }
                });
            });

            CancelExport = ReactiveCommand.Create(() =>
            {
                xmlExportCancellationTokenSource?.Cancel();
            });

            CancelFileCopy = ReactiveCommand.Create(() =>
            {
                fileCopyCancellationTokenSource.Cancel();
            });
        }
    }
}
