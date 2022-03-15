using EBOM_Macro.Converters;
using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;

namespace EBOM_Macro.States
{
    public sealed class SessionState : ReactiveObject, IDisposable
    {
        private bool disposedValue;

        private IDisposable isReadyForExportDisposable, filesToCopyCountDisposable;

        public ProgressState ProgressState { get; private set; }
        public InputState InputState { get; private set; }

        [Reactive] public bool IsReadyForExport { get; private set; }
        [Reactive] public int FilesToCopyCount { get; private set; }

        public ReactiveCommand<(bool, bool), Unit> CopyStats { get; }
        public ReactiveCommand<Item, Unit> CopyAttributes { get; }

        public SessionState()
        {
            ProgressState = new ProgressState();
            InputState = new InputState(ProgressState);

            isReadyForExportDisposable = Observable.CombineLatest(
                InputState.WhenAnyValue(x => x.Items).Select(items => items.Root == null ? Observable.Return<bool>(false) : items.Root.WhenAnyValue(x => x.IsChecked, isChecked => isChecked ?? true)).Switch(),
                InputState.WhenAnyValue(x => x.LDIFolderPath),
                ProgressState.WhenAnyValue(
                    x => x.EBOMReportReadProgress, x => x.EBOMReportReadError,
                    x => x.ExistingDataReadProgress, x => x.ExistingDataReadError,
                    x => x.ComparisonProgress,
                    (ebomReportProgress, ebomReportError, existingDataReadProgress, existingDataReadError, comparisonProgress) =>
                        (ebomReportProgress, ebomReportError, existingDataReadProgress, existingDataReadError, comparisonProgress)
                ),

                (rootIsChecked, ldiPath, progressData) => !rootIsChecked ||
                (progressData.ebomReportProgress.IsInExclusiveRange(0, 1) && !progressData.ebomReportError) ||
                progressData.existingDataReadProgress.IsInExclusiveRange(0, 1) || progressData.existingDataReadError ||
                progressData.comparisonProgress.IsInExclusiveRange(0, 1) ||
                !Directory.Exists(ldiPath) ? false : true
            ).Subscribe(v => IsReadyForExport = v);

            filesToCopyCountDisposable = Observable.CombineLatest(
                this.WhenAnyValue(x => x.IsReadyForExport),
                InputState.WhenAnyValue(x => x.CopyFilesFromPath),
                InputState.StatsState.WhenAnyValue(x => x.SelectedTotalParts),
                InputState.StatsState.WhenAnyValue(x => x.SelectedDeletedParts),
                (isReadyForExport, pathToCopyFilesFrom, selectedPartsCount, selectedDeletedParts) => !isReadyForExport || string.IsNullOrWhiteSpace(pathToCopyFilesFrom) ? 0 : (selectedPartsCount - selectedDeletedParts)
            ).Subscribe(v => FilesToCopyCount = v);

            CopyStats = ReactiveCommand.Create<(bool showSelected, bool showAvailable)>(p =>
            {
                var statsTable = InputState.StatsState.AsTable;

                var outputRows = new List<string>[statsTable[0].Length];

                for (var ci = 0; ci < statsTable.Length; ++ci)
                {
                    var statsTableColumn = statsTable[ci];

                    for (var ri = 0; ri < statsTableColumn.Length; ++ri)
                    {
                        if (ci == 0) outputRows[ri] = new List<string>();

                        var statsValue = statsTable[ci][ri];

                        if (ci > 0 && ri > 0)
                        {
                            (int selected, int available) pairValue = ((int, int))statsValue;

                            if (p.showSelected && p.showAvailable)
                                statsValue = $"{pairValue.selected} / {pairValue.available}";

                            else if (p.showSelected)
                                statsValue = pairValue.selected;

                            else if (p.showAvailable)
                                statsValue = pairValue.available;

                            else
                                statsValue = null;
                        }

                        outputRows[ri].Add($"{statsValue}");
                    }
                }

                Clipboard.SetText(string.Join("\n", outputRows.Select(c => string.Join("\t", c))));
            });

            CopyAttributes = ReactiveCommand.Create<Item>(item =>
            {
                var attributes = new ItemAttributesToViewConverter().Convert(item);

                var columnHeaderValues = new List<string>(attributes.Count);
                var currentValues = new List<string>(columnHeaderValues.Count);
                var newValues = new List<string>(columnHeaderValues.Count);



                foreach (var attribute in attributes)
                {
                    columnHeaderValues.Add($"{attribute.Name}");
                    currentValues.Add($"{attribute.CurrentValue}");
                    newValues.Add($"{attribute.NewValue}");
                }

                var rows = new[]
                {
                    columnHeaderValues.Prepend(""),
                    currentValues.Prepend("Current"),
                    newValues.Prepend("New")
                };

                Clipboard.SetText($"{string.Join("\n", rows.Select(r => string.Join("\t", r)))}");
            });
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CopyStats.Dispose();
                    CopyAttributes.Dispose();

                    isReadyForExportDisposable.Dispose();
                    filesToCopyCountDisposable.Dispose();
                    InputState.Dispose();
                }

                IsReadyForExport = false;
                FilesToCopyCount = 0;

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
