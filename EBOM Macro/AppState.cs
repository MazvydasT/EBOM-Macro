using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using static EBOM_Macro.Item;

namespace EBOM_Macro
{
    public sealed class AppState : ReactiveObject
    {
        public static AppState State { get; } = new AppState();


        [Reactive] public string EBOMReportPath { get; set; }
        [Reactive] public double EBOMReportReadProgress { get; private set; }
        [Reactive] public string EBOMReportReadMessage { get; private set; }

        [Reactive] public string Program { get; set; }

        [Reactive] public string LDIFolderPath { get; set; }

        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public double ExistingDataReadProgress { get; private set; }
        [Reactive] public string ExistingDataReadMessage { get; private set; }

        [Reactive] private CancellationTokenSource ExportDSCancellationTokenSource { get; set; }
        [Reactive] private CancellationTokenSource ExportXMLCancellationTokenSource { get; set; }

        [Reactive] public double ExportProgress { get; private set; }
        [Reactive] public string ExportMessage { get; private set; }

        [ObservableAsProperty] public Visibility ClearExistingDataButtonVisibility { get; private set; }

        [ObservableAsProperty] public Visibility ExportDSButtonVisibility { get; private set; }
        [ObservableAsProperty] public Visibility ExportXMLButtonVisibility { get; private set; }
        [ObservableAsProperty] public Visibility ExportCancelButtonVisibility { get; private set; }
        
        [ObservableAsProperty] public bool ExportButtonsAreActive { get; private set; }

        [ObservableAsProperty] public bool InputsAreActive { get; private set; }

        [ObservableAsProperty] public IEnumerable<Item> RootItems { get; }

        public ReactiveCommand<Unit, Unit> BrowseEBOMReport { get; }
        public ReactiveCommand<Unit, Unit> BrowseLDIFolder { get; }
        public ReactiveCommand<Unit, Unit> BrowseExistingData { get; }
        public ReactiveCommand<Unit, Unit> ClearExistingData { get; }
        public ReactiveCommand<Unit, Unit> SaveDSList { get; }
        public ReactiveCommand<Unit, Unit> SaveXML { get; }
        public ReactiveCommand<Unit, Unit> CancelExport { get; }

        private AppState()
        {
            var itemsObservable = this.WhenAnyValue(x => x.EBOMReportPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path =>
                {
                    return Observable.FromAsync(token => CSVManager.GetItems(path, new Progress<ProgressUpdate>(progress =>
                    {
                        EBOMReportReadProgress = (double)progress.Value / progress.Max;
                        EBOMReportReadMessage = progress.Message;
                    }), token))
                        .Catch((Exception exception) =>
                        {
                            EBOMReportReadMessage = exception.Message;

                            return Observable.Return(new List<Item>(0));
                        })
                        .StartWith(new List<Item>(0));
                })
                .Switch()
                .Replay(1).RefCount();

            var level3PHObservable = itemsObservable.Select(items => CSVManager.CreatePHsForCPSCLevel(items.Where(item => item.Type == ItemType.DS), CSVManager.CPSCLevels.Level3))
                .Replay(1).RefCount();

            var level2PHObservable = level3PHObservable.Select(items => CSVManager.CreatePHsForCPSCLevel(items, CSVManager.CPSCLevels.Level2))
                .Replay(1).RefCount();

            var level1PHObservable = level2PHObservable.Select(items => CSVManager.CreatePHsForCPSCLevel(items, CSVManager.CPSCLevels.Level1))
                .Replay(1).RefCount();

            var rootObservable = level1PHObservable.Select(items =>
            {
                if (items.Count == 0) return Enumerable.Empty<Item>();

                var root = new Item { Type = ItemType.PH };

                root.Children.AddRange(items.Select(i =>
                {
                    i.Parent = root;

                    return i;
                }));

                return root.Yield();
            }).Replay(1).RefCount();

            var programObservable = this.WhenAnyValue(x => x.Program)
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Select(p => p?.Trim().ToUpper() ?? "")
                .DistinctUntilChanged();

            var phsObservable = Observable.CombineLatest(
                programObservable,

                Observable.Zip(
                    level3PHObservable,
                    level2PHObservable,
                    level1PHObservable,
                    rootObservable,
                    (leve3PHs, level2PHs, level1PHs, root) => new { root, level1PHs, level2PHs, leve3PHs }
                ),
                (program, phs) => new { program, phs }
            ).Select(data =>
            {
                var program = data.program;
                
                if (string.IsNullOrWhiteSpace(program)) return new { PHs = Enumerable.Empty<Item>(), Count = 0 };

                var phs = data.phs.root.Concat(data.phs.level1PHs).Concat(data.phs.level2PHs).Concat(data.phs.leve3PHs);
                var root = data.phs.root.FirstOrDefault();

                Utils.AssignNumberNameAndId(phs, root, program).LastOrDefault();

                return new { PHs = phs, Count = 1 + data.phs.level1PHs.Count + data.phs.level2PHs.Count + data.phs.leve3PHs.Count };
            }).Replay(1).RefCount();



            var existingItemsObservable = this.WhenAnyValue(x => x.ExistingDataPath)
                .Select(path =>
                {
                    var blank = new Dictionary<string, Item>(0);

                    if (string.IsNullOrWhiteSpace(path)) return Observable.Return(blank);

                    return Observable.FromAsync(token => CSVManager.DSListToItems(path, new Progress<ProgressUpdate>(progress =>
                    {
                        ExistingDataReadProgress = (double)progress.Value / progress.Max;
                        ExistingDataReadMessage = progress.Message;
                    }), token))
                        .Catch((Exception exception) =>
                        {
                            ExistingDataReadMessage = exception.Message;

                            return Observable.Return(blank);
                        })
                        .StartWith(blank);
                })
                .Switch()
                .Replay(1).RefCount();


            var allItemsObservable = Observable.CombineLatest(phsObservable, itemsObservable, (data, items) => new { Items = data.PHs.Concat(items), Count = data.Count + items.Count })
                .Replay(1).RefCount();

            var stateSetItemsObservable = Observable.CombineLatest(allItemsObservable, existingItemsObservable, (allItems, existingItems) =>
            {
                return Observable.FromAsync(token => Task.Factory.StartNew((cancellationToken) =>
                {
                    if (existingItems.Count == 0)
                    {
                        foreach(var item in allItems.Items)
                        {
                            if(item.Parent == null)
                            {
                                item.IsChecked = true;
                            }

                            item.State = ItemState.New;
                            item.RedundantChildren = null;
                        }
                    }

                    else
                    {
                        var processedWithHierarchy = new HashSet<Item>(allItems.Count);

                        foreach (var item in allItems.Items)
                        {
                            ((CancellationToken)cancellationToken).ThrowIfCancellationRequested();

                            item.IsChecked = false;
                            item.RedundantChildren = null;

                            if (processedWithHierarchy.Contains(item)) continue;

                            item.State = ItemState.New;

                            if (existingItems.TryGetValue(item.ExternalId, out Item matchingItem))
                            {
                                if (item.Type == ItemType.DS && matchingItem.Children.Count == 0)
                                {
                                    ItemState state;

                                    if (item.GetHash() != matchingItem.GetHash()) state = ItemState.Modified;
                                    else state = ItemState.Unchanged;

                                    processedWithHierarchy.UnionWith(item.GetSelfAndDescendants().Select(i =>
                                    {
                                        i.State = state;
                                        i.RedundantChildren = null;

                                        return i;
                                    }));
                                }

                                else if (item.CombinedAttributes != matchingItem.CombinedAttributes)
                                {
                                    item.State = ItemState.Modified;
                                }

                                else
                                {
                                    var childrenHashSet = item.Children.Select(i => i.ExternalId).ToHashSet();
                                    var redundantItems = matchingItem.Children.Where(i => !childrenHashSet.Contains(i.ExternalId)).ToList();

                                    if (redundantItems.Count > 0)
                                    {
                                        item.State = ItemState.Modified;
                                        item.RedundantChildren = redundantItems;
                                    }

                                    else
                                    {
                                        item.State = ItemState.Unchanged;
                                    }
                                }
                            }
                        }

                        foreach (var item in allItems.Items)
                        {
                            ((CancellationToken)cancellationToken).ThrowIfCancellationRequested();

                            if (item.State == ItemState.Unchanged)
                            {
                                if (item.GetSelfAndDescendants().Where(i => i.State == ItemState.Modified).Count() > 0)
                                {
                                    item.State = ItemState.HasModifiedDescendants;
                                }
                            }

                            if (item.State == ItemState.New || item.State == ItemState.Modified)
                            {
                                item.SelectWithoutDescendants.Execute(null);
                            }
                        }
                    }

                    return allItems;
                }, token, token));//.StartWith(new { Items = Enumerable.Empty<Item>(), Count = 0 });
            }).Switch()/*.Replay(1).RefCount()*/.Subscribe();

            this.WhenAnyValue(x => x.ExistingDataPath)
                .Select(v => string.IsNullOrWhiteSpace(v) ? Visibility.Collapsed : Visibility.Visible)
                .ToPropertyEx(this, x => x.ClearExistingDataButtonVisibility);

            phsObservable.Select(data => data.Count > 0 ? rootObservable : Observable.Return(Enumerable.Empty<Item>()))
                .DistinctUntilChanged()
                .Switch()
                .ToPropertyEx(this, x => x.RootItems);

            var exportingDSListObservable = this.WhenAnyValue(x => x.ExportDSCancellationTokenSource).Select(cts => cts != null).Replay(1).RefCount();
            var exportingXMLObserbavle = this.WhenAnyValue(x => x.ExportXMLCancellationTokenSource).Select(cts => cts != null).Replay(1).RefCount();

            var exportIsRunningObservable = Observable.CombineLatest(exportingDSListObservable, exportingXMLObserbavle, (exportingDSList, exportingXML) => exportingDSList || exportingXML)
                .Replay(1).RefCount();

            Observable.CombineLatest(
                phsObservable.Select(data => data.Count > 0),
                exportIsRunningObservable,
                this.WhenAnyValue(x => x.LDIFolderPath).Select(v => Directory.Exists(v)),
                
                (phsHasData, exportIsRunning, ldiDirectoryExists) => phsHasData && !exportIsRunning && ldiDirectoryExists
            ).ToPropertyEx(this, x => x.ExportButtonsAreActive);

            exportIsRunningObservable.Select(v => !v)
                .ToPropertyEx(this, x => x.InputsAreActive);

            exportingDSListObservable.Select(v => v ? Visibility.Collapsed : Visibility.Visible).ToPropertyEx(this, x => x.ExportDSButtonVisibility);
            exportingXMLObserbavle.Select(v => v ? Visibility.Collapsed : Visibility.Visible).ToPropertyEx(this, x => x.ExportXMLButtonVisibility);

            exportIsRunningObservable.Select(v => v ? Visibility.Visible : Visibility.Collapsed).ToPropertyEx(this, x => x.ExportCancelButtonVisibility);

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
                ExistingDataReadMessage = "";
                ExistingDataReadProgress = 0;
            });

            SaveDSList = ReactiveCommand.Create(() =>
            {
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

                        allItemsObservable.Take(1).Subscribe(async data =>
                        {
                            using (ExportDSCancellationTokenSource = new CancellationTokenSource())
                            {
                                try
                                {
                                    await CSVManager.ItemsToDSList(dialog.FileName, data.Items, data.Count, new Progress<ProgressUpdate>(progress =>
                                    {
                                        ExportProgress = (double)progress.Value / progress.Max;
                                        ExportMessage = progress.Message;
                                    }), ExportDSCancellationTokenSource.Token);
                                }

                                catch (OperationCanceledException) { }

                                catch (Exception exception)
                                {
                                    ExportMessage = exception.Message;
                                }

                                finally
                                {
                                    ExportDSCancellationTokenSource = null;
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
                            allItemsObservable,
                            this.WhenAnyValue(x => x.LDIFolderPath),

                            (data, ldiPath) => new { data, ldiPath }
                        ).Take(1).Subscribe(async pair =>
                        {
                            using (ExportXMLCancellationTokenSource = new CancellationTokenSource())
                            {
                                try
                                {
                                    await XMLManager.ItemsToXML(dialog.FileName, pair.data.Items, pair.data.Count, pair.ldiPath, new Progress<ProgressUpdate>(progress =>
                                    {
                                        ExportProgress = (double)progress.Value / progress.Max;
                                        ExportMessage = progress.Message;
                                    }), ExportXMLCancellationTokenSource.Token);
                                }

                                catch (OperationCanceledException) { }

                                catch (Exception exception)
                                {
                                    ExportMessage = exception.Message;
                                }

                                finally
                                {
                                    ExportXMLCancellationTokenSource = null;
                                }
                            }
                        });
                    }
                }
            });

            CancelExport = ReactiveCommand.Create(() =>
            {
                ExportDSCancellationTokenSource?.Cancel();
                ExportXMLCancellationTokenSource?.Cancel();

                ExportDSCancellationTokenSource = null;
                ExportXMLCancellationTokenSource = null;
            });
        }
    }
}