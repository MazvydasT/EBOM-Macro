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

        IObservable<IChangeSet<SessionState>> sessionsChangeSetObservable;

        CancellationTokenSource cancellationTokenSource;

        public OutputState(IObservable<IChangeSet<SessionState>> sessionsChangeSetObservable)
        {
            this.sessionsChangeSetObservable = sessionsChangeSetObservable.Replay(1).RefCount();

            this.sessionsChangeSetObservable.AutoRefresh(s => s.IsReadyForExport)
                .ToCollection().Select(c => c.Take(c.Count - 1).All(s => s.IsReadyForExport))
                .ToPropertyEx(this, x => x.AllSessionsAreReadyForExport);

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
                    
                    this.sessionsChangeSetObservable.Transform(s => s.InputState).ToCollection().Take(1).Subscribe(async sessions =>
                    {
                        ExportProgress = 0;
                        ExportMessage = "";
                        ExportError = false;

                        var sessionsList = sessions.Take(sessions.Count - 1).Select(s => new XMLExportData { Items = s.Items, ExternalIdPrefix = s.ExternalIdPrefix, LDIFolderPath = s.LDIFolderPath }).ToList();

                        using (cancellationTokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                await XMLManager.ItemsToXML(dialog.FileName, sessionsList, new Progress<ProgressUpdate>(progress =>
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
