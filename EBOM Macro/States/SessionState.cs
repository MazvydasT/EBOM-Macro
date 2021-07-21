using EBOM_Macro.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.IO;
using System.Reactive.Linq;

namespace EBOM_Macro.States
{
    public sealed class SessionState : ReactiveObject
    {
        public ProgressState ProgressState { get; }
        public InputState InputState { get; }

        [ObservableAsProperty] public bool IsReadyForExport { get; }

        public SessionState()
        {
            ProgressState = new ProgressState();
            InputState = new InputState(ProgressState);

            Observable.CombineLatest(
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
            ).ToPropertyEx(this, x => x.IsReadyForExport);
        }
    }
}
