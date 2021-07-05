using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EBOM_Macro.States
{
    public class OutputState : ReactiveObject
    {
        public ReactiveCommand<Unit, Unit> SaveDSList { get; }
        public ReactiveCommand<Unit, Unit> SaveXML { get; }
        public ReactiveCommand<Unit, Unit> CancelExport { get; }

        InputState inputState;
        ProgressState progressState;

        CancellationTokenSource cancellationTokenSource;

        public OutputState(InputState inputState, ProgressState progressState)
        {
            this.inputState = inputState;
            this.progressState = progressState;

            SaveDSList = ReactiveCommand.Create(() =>
            {
                progressState.ExportError = false;
                progressState.ExportMessage = "";
                progressState.ExportProgress = 0;

                var properties = Properties.Settings.Default;

                var lastUsedDSListDirectory = string.IsNullOrWhiteSpace(properties.LastUsedDSListDirectory) ? Environment.CurrentDirectory : properties.LastUsedDSListDirectory;

                using (var dialog = new SaveFileDialog
                {
                    Filter = "CSV (Comma delimited) (*.csv)|*.csv",
                    InitialDirectory = lastUsedDSListDirectory,
                    Title = "Save DS list",
                    OverwritePrompt = true
                })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        properties.LastUsedDSListDirectory = Path.GetDirectoryName(dialog.FileName);
                        properties.Save();

                        Observable.CombineLatest(inputState.WhenAnyValue(x => x.Items), inputState.ExternalIdPrefixObservable, (items, externalIdPrefix) => (items, externalIdPrefix))
                            .Take(1).Subscribe(async data =>
                            {
                                progressState.ExportProgress = 0;
                                progressState.ExportMessage = "";
                                progressState.ExportError = false;

                                using (cancellationTokenSource = new CancellationTokenSource())
                                {
                                    try
                                    {
                                        await CSVManager.WriteDSList(dialog.FileName, (data.items.Root, data.items.PHs), data.externalIdPrefix, new Progress<ProgressUpdate>(progress =>
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
                }
            });

            SaveXML = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedXMLDirectory = string.IsNullOrWhiteSpace(properties.LastUsedXMLDirectory) ? Environment.CurrentDirectory : properties.LastUsedXMLDirectory;

                using (var dialog = new SaveFileDialog
                {
                    Filter = "eM-Planner data (*.xml)|*.xml",
                    InitialDirectory = lastUsedXMLDirectory,
                    Title = "Save XML",
                    OverwritePrompt = true
                })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        properties.LastUsedXMLDirectory = Path.GetDirectoryName(dialog.FileName);
                        properties.Save();

                        Observable.CombineLatest(
                            inputState.WhenAnyValue(x => x.Items),
                            inputState.ExternalIdPrefixObservable,
                            inputState.ExistingDataObservable,
                            inputState.WhenAnyValue(x => x.LDIFolderPath),

                            (items, externalIdPrefix, existingData, ldiPath) => (items, externalIdPrefix, existingData, ldiPath)
                        ).Take(1).Subscribe(async data =>
                        {
                            progressState.ExportProgress = 0;
                            progressState.ExportMessage = "";
                            progressState.ExportError = false;

                            using (cancellationTokenSource = new CancellationTokenSource())
                            {
                                try
                                {
                                    await XMLManager.ItemsToXML(dialog.FileName, data.items, data.externalIdPrefix, data.existingData.ExternalIdPrefix, data.ldiPath, new Progress<ProgressUpdate>(progress =>
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
                }
            });

            CancelExport = ReactiveCommand.Create(() =>
            {
                cancellationTokenSource?.Cancel();
            });
        }
    }
}
