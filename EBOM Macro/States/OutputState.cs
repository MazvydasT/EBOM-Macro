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

namespace EBOM_Macro.States
{
    public class OutputState : ReactiveObject
    {
        public ReactiveCommand<Unit, Unit> SaveXML { get; }
        public ReactiveCommand<Unit, Unit> CancelExport { get; }

        InputState inputState;
        ProgressState progressState;

        CancellationTokenSource cancellationTokenSource;

        public OutputState(InputState inputState, ProgressState progressState)
        {
            this.inputState = inputState;
            this.progressState = progressState;
            
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

                    Observable.CombineLatest(
                        inputState.WhenAnyValue(x => x.Items),
                        inputState.ExternalIdPrefixObservable,
                        inputState.WhenAnyValue(x => x.LDIFolderPath),

                        (items, externalIdPrefix, ldiPath) => (items, externalIdPrefix, ldiPath)
                    ).Take(1).Subscribe(async data =>
                    {
                        progressState.ExportProgress = 0;
                        progressState.ExportMessage = "";
                        progressState.ExportError = false;

                        using (cancellationTokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                await XMLManager.ItemsToXML(dialog.FileName, data.items, data.externalIdPrefix, data.ldiPath, new Progress<ProgressUpdate>(progress =>
                                {
                                    progressState.ExportProgress = (double)progress.Value / progress.Max;
                                    progressState.ExportMessage = progress.Message;
                                }), cancellationTokenSource.Token);
                            }

                            catch (OperationCanceledException)
                            {
                                progressState.ExportProgress = 0;
                                progressState.ExportMessage = "";
                                progressState.ExportError = false;
                            }

                            catch (Exception exception)
                            {
                                progressState.ExportMessage = exception.Message;
                                progressState.ExportError = true;
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
