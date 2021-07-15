using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Win32;
using ReactiveUI.Fody.Helpers;
using DynamicData;
using System.Collections.Generic;

namespace EBOM_Macro.States
{
    public class OutputState : ReactiveObject
    {
        [Reactive] public double ExportProgress { get; set; }
        [Reactive] public string ExportMessage { get; set; }
        [Reactive] public bool ExportError { get; set; }

        [ObservableAsProperty] public bool AllSessionsAreReadyForExport { get; }

        public ReactiveCommand<Unit, Unit> SaveXML { get; }
        public ReactiveCommand<Unit, Unit> CancelExport { get; }

        IObservable<IReadOnlyCollection<SessionState>> sessionsChangeSetObservable;

        CancellationTokenSource cancellationTokenSource;

        public OutputState(IObservable<IChangeSet<SessionState>> sessionsChangeSetObservable)
        {
            this.sessionsChangeSetObservable = sessionsChangeSetObservable.AutoRefresh(s => s.IsReadyForExport)
                .ToCollection().Replay(1).RefCount();

            this.sessionsChangeSetObservable.Select(c => c.Count > 0 && c.All(s => s.IsReadyForExport)).ToPropertyEx(this, x => x.AllSessionsAreReadyForExport);

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

                        var exportDataList = sessions.Select(s => s.InputState).Select(i => new XMLExportData { Items = i.Items, ExternalIdPrefix = i.ExternalIdPrefix, LDIFolderPath = i.LDIFolderPath }).ToList();

                        using (cancellationTokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                await XMLManager.ItemsToXML(dialog.FileName, exportDataList, new Progress<ProgressUpdate>(progress =>
                                {
                                    ExportProgress = (double)progress.Value / progress.Max;
                                    ExportMessage = progress.Message;
                                }), cancellationTokenSource.Token);
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
                                cancellationTokenSource = null;
                            }
                        }
                    });
                }
            });

            CancelExport = ReactiveCommand.Create(() =>
            {
                cancellationTokenSource?.Cancel();
            });
        }
    }
}
