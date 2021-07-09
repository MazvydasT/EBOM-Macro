using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Interop;

namespace EBOM_Macro.States
{
    public class InputState : ReactiveObject
    {
        [Reactive] public string EBOMReportPath { get; set; }
        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public string LDIFolderPath { get; set; }
        [Reactive] public string ExternalIdPrefixInput { get; set; }

        [ObservableAsProperty] public ItemsContainer Items { get; }
        [ObservableAsProperty] public string ExternalIdPrefix { get; }

        public ReactiveCommand<Unit, Unit> BrowseEBOMReport { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseLDIFolder { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseExistingData { get; private set; }
        public ReactiveCommand<Unit, Unit> ClearExistingData { get; private set; }

        //public IObservable<string> ExternalIdPrefixObservable { get; private set; }

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
                .Select(path => Observable.FromAsync(token => XMLManager.ReadExistingData(path, new Progress<ProgressUpdate>(progress =>
                {
                    progressState.ExistingDataReadProgress = (double)progress.Value / progress.Max;
                    progressState.ExistingDataReadMessage = progress.Message;

                }), token)).Catch((Exception exception) =>
                {
                    progressState.ExistingDataReadMessage = exception.Message;
                    progressState.ExistingDataReadError = true;

                    return Observable.Return<Dictionary<string, Item>>(default);
                })).Switch();

            //ExternalIdPrefixObservable = this.WhenAnyValue(x => x.ExternalIdPrefixInput)
            this.WhenAnyValue(x => x.ExternalIdPrefixInput)
                .Throttle(TimeSpan.FromMilliseconds(400))
                .Select(prefix => prefix?.Trim().ToUpper() ?? "")
                .ToPropertyEx(this, x => x.ExternalIdPrefix);
                //.Replay(1).RefCount();

            Observable.CombineLatest(
                itemsObservable,

                Observable.CombineLatest(existingDataObservable, /*ExternalIdPrefixObservable*/ this.WhenAnyValue(x => x.ExternalIdPrefix), (existingData, externalIdPrefix) => (existingData, externalIdPrefix: existingData == null ? "" : externalIdPrefix))
                    .DistinctUntilChanged(),

                (items, pair) => (items, pair.existingData, pair.externalIdPrefix))
                .ObserveOn(TaskPoolScheduler.Default)
                .Select(data =>
                {
                    previousComparisonTaskData.CancellationTokenSource?.Cancel();
                    previousComparisonTaskData.Task?.Wait();

                    progressState.ComparisonProgress = 0;

                    return data;
                })
                .ObserveOnDispatcher()
                .Select(data =>
                {
                    var cancellationTokenSource = new CancellationTokenSource();

                    var task = ItemManager.SetStatus(data.items, data.existingData, data.externalIdPrefix, new Progress<ProgressUpdate>(progress =>
                    {
                        progressState.ComparisonProgress = (double)progress.Value / progress.Max;
                    }), cancellationTokenSource.Token);

                    previousComparisonTaskData = (task, cancellationTokenSource);

                    return Observable.FromAsync(() => task);
                })
                .Switch().ToPropertyEx(this, x => x.Items);
        }

        void InitializeCommands()
        {
            BrowseEBOMReport = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedEBOMReportDirectory = string.IsNullOrWhiteSpace(properties.LastUsedEBOMReportDirectory) ?
                    (string.IsNullOrWhiteSpace(EBOMReportPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(EBOMReportPath)) : properties.LastUsedEBOMReportDirectory;

                var dialog = new OpenFileDialog
                {
                    Filter = "CSV (Comma delimited) (*.csv)|*.csv",
                    InitialDirectory = lastUsedEBOMReportDirectory,
                    Multiselect = false,
                    CheckFileExists = true,
                    Title = "Select EBOM report"
                };

                if (dialog.ShowDialog() == true)
                {
                    EBOMReportPath = Utils.PathToUNC(dialog.FileName);
                    properties.LastUsedEBOMReportDirectory = Path.GetDirectoryName(EBOMReportPath);
                    properties.Save();
                }
            });

            BrowseLDIFolder = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedLDIDirectory = string.IsNullOrWhiteSpace(properties.LastUsedLDIDirectory) ?
                    (string.IsNullOrWhiteSpace(LDIFolderPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(LDIFolderPath)) : properties.LastUsedLDIDirectory;

                var dialog = new FolderSelect.FolderSelectDialog
                {
                    InitialDirectory = lastUsedLDIDirectory,
                    Title = "Select LDI folder"
                };

                if (dialog.ShowDialog(new WindowInteropHelper(App.Current.MainWindow).Handle))
                {
                    LDIFolderPath = Utils.PathToUNC(dialog.FileName);
                    properties.LastUsedLDIDirectory = LDIFolderPath;
                    properties.Save();
                }
            });

            BrowseExistingData = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedDSListDirectory = string.IsNullOrWhiteSpace(properties.LastUsedDSListDirectory) ? Environment.CurrentDirectory : properties.LastUsedDSListDirectory;

                var dialog = new OpenFileDialog
                {
                    Filter = "eM-Planner data (*.xml)|*.xml",
                    InitialDirectory = lastUsedDSListDirectory,
                    Multiselect = false,
                    CheckFileExists = true,
                    Title = "Select existing data"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExistingDataPath = Utils.PathToUNC(dialog.FileName);
                    properties.LastUsedDSListDirectory = Path.GetDirectoryName(ExistingDataPath);
                    properties.Save();
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