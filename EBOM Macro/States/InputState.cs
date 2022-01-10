using EBOM_Macro.Managers;
using EBOM_Macro.Models;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Interop;

namespace EBOM_Macro.States
{
    public class InputState : ReactiveObject, IDisposable
    {
        bool systemRootRelativePath = Properties.Settings.Default.UseSystemRootRelativePath;
        public bool SystemRootRelativePath
        {
            get => systemRootRelativePath;
            set
            {
                this.RaiseAndSetIfChanged(ref systemRootRelativePath, value);

                var properties = Properties.Settings.Default;

                if (properties.UseSystemRootRelativePath != value)
                {
                    properties.UseSystemRootRelativePath = value;
                    properties.Save();
                }
            }
        }

        [Reactive] public string EBOMReportPath { get; set; }
        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public string SystemRootFolderPath { get; set; } = Properties.Settings.Default.LastUsedSystemRootDirectory;
        [Reactive] public string LDIFolderPath { get; set; }
        [Reactive] public bool ReuseExternalIds { get; set; } = true;
        [Reactive] public string ExternalIdPrefixInput { get; set; }
        [Reactive] public bool ComFoxTranslationSystemIsUsed { get; set; } = false;

        [Reactive] public ItemsContainer Items { get; private set; }
        [Reactive] public Item[] Root { get; private set; }
        [Reactive] public string ExternalIdPrefix { get; private set; }
        [Reactive] public string AdjustedLDIPath { get; private set; }

        public ReactiveCommand<Unit, Unit> BrowseEBOMReport { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseSystemRootFolder { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseLDIFolder { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseExistingData { get; private set; }
        public ReactiveCommand<Unit, Unit> ClearExistingData { get; private set; }

        ProgressState progressState;
        public StatsState StatsState { get; set; } = new StatsState();

        IDisposable itemsDisposable, externalIdPrefixDisposable, adjustedLDIPathDisposable;

        CancellationTokenSource cancellationTokenSource = null;
        BehaviorSubject<bool> releaseLastValue = new BehaviorSubject<bool>(true);

        private bool disposedValue;

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

            adjustedLDIPathDisposable = Observable.CombineLatest(
                this.WhenAnyValue(x => x.SystemRootRelativePath),
                this.WhenAnyValue(x => x.SystemRootFolderPath),
                this.WhenAnyValue(x => x.LDIFolderPath),

                (isSystemRootRelative, systemRootFolderPath, ldiFolderPath) =>
                    !isSystemRootRelative ? ldiFolderPath :
                        (!string.IsNullOrEmpty(systemRootFolderPath) && (ldiFolderPath?.StartsWith(systemRootFolderPath) ?? false) ?
                            ldiFolderPath.Replace(systemRootFolderPath, "#") : null)
            ).Subscribe(v => AdjustedLDIPath = v);

            externalIdPrefixDisposable = this.WhenAnyValue(x => x.ExternalIdPrefixInput)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(prefix => prefix?.Trim().ToUpper() ?? "")
                .Subscribe(v => ExternalIdPrefix = v);

            itemsDisposable = Observable.CombineLatest(
                itemsObservable,
                this.WhenAnyValue(x => x.AdjustedLDIPath),

                Observable.CombineLatest(
                    existingDataObservable,
                    this.WhenAnyValue(x => x.ExternalIdPrefix),
                    this.WhenAnyValue(x => x.ReuseExternalIds),
                    this.WhenAnyValue(x => x.ComFoxTranslationSystemIsUsed),
                    (existingData, externalIdPrefix, reuseExtIds, comFoxTranslationSystemIsUsed) =>
                        (existingData, externalIdPrefix: existingData == null ? "" : externalIdPrefix, reuseExternalIds: existingData == null ? false : reuseExtIds, comFoxTranslationSystemIsUsed)
                ).DistinctUntilChanged(),

                (items, ldiFolderPath, tuple) => (items: string.IsNullOrEmpty(ldiFolderPath) ? default : items, ldiFolderPath, tuple.existingData, tuple.externalIdPrefix, tuple.reuseExternalIds, tuple.comFoxTranslationSystemIsUsed))
                .ObserveOn(TaskPoolScheduler.Default)
                .Throttle(data =>
                {
                    lock (this)
                    {
                        cancellationTokenSource?.Cancel();
                    }

                    return releaseLastValue;
                })
                .ObserveOnDispatcher()
                .Select(data =>
                {
                    lock (this)
                    {
                        progressState.ComparisonProgress = 0;

                        cancellationTokenSource = new CancellationTokenSource();

                        var task = ItemManager.SetStatus(data.items, data.existingData, data.externalIdPrefix, data.reuseExternalIds, data.comFoxTranslationSystemIsUsed, data.ldiFolderPath, new Progress<ProgressUpdate>(progress =>
                        {
                            progressState.ComparisonProgress = (double)progress.Value / progress.Max;
                        }), cancellationTokenSource.Token);

                        return Observable.FromAsync(() => task).Do(_ => releaseLastValue.OnNext(true));
                    }
                })
                .Switch()
                .Subscribe(v =>
                {
                    ItemManager.UpdateStats(v, StatsState);

                    Items = v;
                    Root = v.Root == null ? new Item[0] : new[] { v.Root };
                });
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

            BrowseSystemRootFolder = ReactiveCommand.Create(() =>
            {
                var properties = Properties.Settings.Default;

                var lastUsedSystemRootDirectory = string.IsNullOrWhiteSpace(properties.LastUsedSystemRootDirectory) ?
                    (string.IsNullOrWhiteSpace(SystemRootFolderPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(SystemRootFolderPath)) : properties.LastUsedSystemRootDirectory;

                var dialog = new FolderSelect.FolderSelectDialog
                {
                    InitialDirectory = lastUsedSystemRootDirectory,
                    Title = "Select System Root folder"
                };

                if (dialog.ShowDialog(new WindowInteropHelper(App.Current.MainWindow).Handle))
                {
                    SystemRootFolderPath = Utils.PathToUNC(dialog.FileName);
                    properties.LastUsedSystemRootDirectory = SystemRootFolderPath;
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    BrowseEBOMReport.Dispose();
                    BrowseExistingData.Dispose();
                    BrowseSystemRootFolder.Dispose();
                    BrowseLDIFolder.Dispose();
                    ClearExistingData.Dispose();

                    lock (this)
                    {
                        cancellationTokenSource?.Cancel();
                    }

                    itemsDisposable.Dispose();
                    externalIdPrefixDisposable.Dispose();
                    adjustedLDIPathDisposable.Dispose();

                    releaseLastValue.Dispose();
                }

                EBOMReportPath = null;
                ExistingDataPath = null;
                SystemRootFolderPath = null;
                LDIFolderPath = null;
                ReuseExternalIds = default;
                ExternalIdPrefixInput = null;

                Items = default;
                Root = null;
                ExternalIdPrefix = null;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}