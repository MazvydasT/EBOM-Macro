using EBOM_Macro.Extensions;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EBOM_Macro.States
{
    public sealed class InputState : ReactiveObject
    {
        public static InputState State { get; } = new InputState();

        [Reactive] public string EBOMReportPath { get; set; }// = @"C:\Users\mtadara1.JLRIEU1\Desktop\EMS_EBOM_Report_L663_23_05_2021_17_51.csv";
        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public string LDIFolderPath { get; set; }
        [Reactive] public string ExternalIdPrefix { get; set; }

        [ObservableAsProperty] public Item2 Root { get; }

        public ReactiveCommand<Unit, Unit> BrowseEBOMReport { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseLDIFolder { get; private set; }
        public ReactiveCommand<Unit, Unit> BrowseExistingData { get; private set; }
        public ReactiveCommand<Unit, Unit> ClearExistingData { get; private set; }

        Task<Items2Container> previousComparisonTask = null;

        private InputState()
        {
            InitializeObservables();

            InitializeCommands();
        }

        void InitializeObservables()
        {
            var itemsObservable = this.WhenAnyValue(x => x.EBOMReportPath)
                .Do(_ => ProgressState.State.EBOMReportReadError = false)
                .Select(path => Observable.FromAsync(token => CSVManager2.ReadEBOMReport(path, new Progress<ProgressUpdate>(progress =>
                {
                    ProgressState.State.EBOMReportReadProgress = (double)progress.Value / progress.Max;
                    ProgressState.State.EBOMReportReadMessage = progress.Message;
                }), token)).Catch((Exception exception) =>
                {
                    ProgressState.State.EBOMReportReadMessage = exception.Message;
                    ProgressState.State.EBOMReportReadError = true;

                    return Observable.Return<Items2Container>(default);
                })).Switch();

            var existingDataObservable = this.WhenAnyValue(x => x.ExistingDataPath)
                .Do(_ => ProgressState.State.ExistingDataReadError = false)
                .Select(path => Observable.FromAsync(token => CSVManager2.ReadDSList(path, new Progress<ProgressUpdate>(progress =>
                {
                    ProgressState.State.ExistingDataReadProgress = (double)progress.Value / progress.Max;
                    ProgressState.State.ExistingDataReadMessage = progress.Message;
                
                }), token)).Catch((Exception exception) =>
                {
                    ProgressState.State.ExistingDataReadMessage = exception.Message;
                    ProgressState.State.ExistingDataReadError = true;

                    return Observable.Return<IReadOnlyDictionary<string, Item2>>(default);
                })).Switch();

            var externalIdPrefixObservable = this.WhenAnyValue(x => x.ExternalIdPrefix).Throttle(TimeSpan.FromMilliseconds(200)).Select(prefix => prefix?.Trim().ToUpper() ?? "");

            var itemsWithStateObservable = Observable.CombineLatest(
                itemsObservable,

                Observable.CombineLatest(existingDataObservable, externalIdPrefixObservable, (existingData, externalIdPrefix) => (existingData, externalIdPrefix: existingData == null ? "" : externalIdPrefix))
                    .DistinctUntilChanged(),

                (items, pair) =>
                {
                    previousComparisonTask?.Wait();

                    return Observable.FromAsync(token =>
                    {
                        previousComparisonTask = Item2Manager.SetStatus(items, pair.existingData, pair.externalIdPrefix, new Progress<ProgressUpdate>(progress =>
                        {
                            ProgressState.State.ComparisonProgress = (double)progress.Value / progress.Max;
                        }), token);
                        return previousComparisonTask;
                    });
                }).Switch().Replay(1).RefCount();

            itemsWithStateObservable.Select(i => i.Root).ToPropertyEx(this, x => x.Root);
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
                ProgressState.State.ExistingDataReadMessage = "";
                ProgressState.State.ExistingDataReadProgress = 0;
            });
        }
    }
}