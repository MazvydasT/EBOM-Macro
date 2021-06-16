using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EBOM_Macro.States
{
    public class InputState : ReactiveObject
    {
        [Reactive] public string EBOMReportPath { get; set; }// = @"C:\Users\mtadara1.JLRIEU1\Desktop\EMS_EBOM_Report_L663_23_05_2021_17_51.csv";
        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public string LDIFolderPath { get; set; }
        [Reactive] public string ExternalIdPrefix { get; set; }

        [ObservableAsProperty] public ItemsContainer Items { get; }

        public ReactiveCommand<Unit, Unit> BrowseEBOMReport { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseLDIFolder { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseExistingData { get; private set; }
        public ReactiveCommand<Unit, Unit> ClearExistingData { get; private set; }

        public IObservable<string> ExternalIdPrefixObservable { get; private set; }

        ProgressState progressState;

        (Task<ItemsContainer> Task, CancellationTokenSource CancellationTokenSource) previousComparisonTaskData;

        public InputState(ProgressState progressState)
        {
            this.progressState = progressState;

            InitializeObservables();

            InitializeCommands();
        }

        void InitializeObservables()
        {
            var itemsObservable = this.WhenAnyValue(x => x.EBOMReportPath)
                .Do(_ =>
                {
                    progressState.EBOMReportReadError = false;
                    progressState.EBOMReportReadMessage = "";
                    progressState.EBOMReportReadProgress = 0;
                })
                .Select(path => Observable.FromAsync(token => CSVManager.ReadEBOMReport(path, new Progress<ProgressUpdate>(progress =>
                {
                    progressState.EBOMReportReadProgress = (double)progress.Value / progress.Max;
                    progressState.EBOMReportReadMessage = progress.Message;
                }), token)).Catch((Exception exception) =>
                {
                    progressState.EBOMReportReadMessage = exception.Message;
                    progressState.EBOMReportReadError = true;

                    return Observable.Return<ItemsContainer>(default);
                })).Switch();

            var existingDataObservable = this.WhenAnyValue(x => x.ExistingDataPath)
                .Do(_ =>
                {
                    progressState.ExistingDataReadError = false;
                    progressState.ExistingDataReadMessage = "";
                    progressState.ExistingDataReadProgress = 0;
                })
                .Select(path => Observable.FromAsync(token => CSVManager.ReadDSList(path, new Progress<ProgressUpdate>(progress =>
                {
                    progressState.ExistingDataReadProgress = (double)progress.Value / progress.Max;
                    progressState.ExistingDataReadMessage = progress.Message;

                }), token)).Catch((Exception exception) =>
                {
                    progressState.ExistingDataReadMessage = exception.Message;
                    progressState.ExistingDataReadError = true;

                    return Observable.Return<ExistingDataContainer>(default);
                })).Switch();

            ExternalIdPrefixObservable = this.WhenAnyValue(x => x.ExternalIdPrefix)
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Select(prefix => prefix?.Trim().ToUpper() ?? "")
                .Replay(1).RefCount();

            Observable.CombineLatest(
                itemsObservable,

                Observable.CombineLatest(existingDataObservable, ExternalIdPrefixObservable, (existingData, externalIdPrefix) => (existingData, externalIdPrefix: existingData.Items == null ? "" : externalIdPrefix))
                    .DistinctUntilChanged(),

                (items, pair) =>
                {
                    previousComparisonTaskData.CancellationTokenSource?.Cancel();
                    previousComparisonTaskData.Task?.Wait();

                    progressState.ComparisonProgress = 0;

                    return Observable.FromAsync(() =>
                    {
                        var cancellationTokenSource = new CancellationTokenSource();

                        previousComparisonTaskData = (ItemManager.SetStatus(items, pair.existingData, pair.externalIdPrefix, new Progress<ProgressUpdate>(progress =>
                        {
                            progressState.ComparisonProgress = (double)progress.Value / progress.Max;
                        }), cancellationTokenSource.Token), cancellationTokenSource);
                        return previousComparisonTaskData.Task;
                    });
                }).Switch().ToPropertyEx(this, x => x.Items);
        }

        void InitializeCommands()
        {
            BrowseEBOMReport = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedEBOMReportDirectory = string.IsNullOrWhiteSpace(properties.LastUsedEBOMReportDirectory) ?
                    (string.IsNullOrWhiteSpace(EBOMReportPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(EBOMReportPath)) : properties.LastUsedEBOMReportDirectory;

                using (var dialog = new OpenFileDialog
                {
                    Filter = "CSV (Comma delimited) (*.csv)|*.csv",
                    InitialDirectory = lastUsedEBOMReportDirectory,
                    Multiselect = false,
                    CheckFileExists = true,
                    Title = "Select EBOM report"
                })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        EBOMReportPath = Utils.PathToUNC(dialog.FileName);
                        properties.LastUsedEBOMReportDirectory = Path.GetDirectoryName(EBOMReportPath);
                        properties.Save();
                    }
                }
            });

            BrowseLDIFolder = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedLDIDirectory = string.IsNullOrWhiteSpace(properties.LastUsedLDIDirectory) ?
                    (string.IsNullOrWhiteSpace(LDIFolderPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(LDIFolderPath)) : properties.LastUsedLDIDirectory;

                using (var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    InitialDirectory = lastUsedLDIDirectory,
                    Title = "Select LDI folder"
                })
                {
                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        LDIFolderPath = Utils.PathToUNC(dialog.FileName);
                        properties.LastUsedLDIDirectory = LDIFolderPath;
                        properties.Save();
                    }
                }
            });

            BrowseExistingData = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedDSListDirectory = string.IsNullOrWhiteSpace(properties.LastUsedDSListDirectory) ? Environment.CurrentDirectory : properties.LastUsedDSListDirectory;

                using (var dialog = new OpenFileDialog
                {
                    Filter = "CSV (Comma delimited) (*.csv)|*.csv",
                    InitialDirectory = lastUsedDSListDirectory,
                    Multiselect = false,
                    CheckFileExists = true,
                    Title = "Select DS list"
                })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        ExistingDataPath = Utils.PathToUNC(dialog.FileName);
                        properties.LastUsedDSListDirectory = Path.GetDirectoryName(ExistingDataPath);
                        properties.Save();
                    }
                }
            });

            ClearExistingData = ReactiveCommand.Create(() =>
            {
                ExistingDataPath = "";
                progressState.ExistingDataReadMessage = "";
                progressState.ExistingDataReadProgress = 0;
            });
        }
    }
}