using EBOM_Macro.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Reactive.Linq;

namespace EBOM_Macro.States
{
    public sealed class SessionState : ReactiveObject, IDisposable
    {
        private bool disposedValue;

        private IDisposable isReadyForExportDisposable;

        public ProgressState ProgressState { get; private set; }
        public InputState InputState { get; private set; }

        [Reactive] public bool IsReadyForExport { get; private set; }

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
            //).ToPropertyEx(this, x => x.IsReadyForExport);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    isReadyForExportDisposable.Dispose();
                    InputState.Dispose();
                }

                IsReadyForExport = false;

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
