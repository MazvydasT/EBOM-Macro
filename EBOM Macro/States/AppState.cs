using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Linq;
using DynamicData;
using EBOM_Macro.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace EBOM_Macro.States
{
    public sealed class AppState : ReactiveObject
    {
        public static AppState State { get; } = new AppState();

        private SourceList<SessionState> sessionsSourceList = new SourceList<SessionState>();
        private ReadOnlyObservableCollection<SessionState> sessions;
        public ReadOnlyObservableCollection<SessionState> Sessions => sessions;
        
        public OutputState OutputState { get; }

        private AppState()
        {
            var sessionsObservable = sessionsSourceList.Connect().RefCount();

            sessionsObservable.ObserveOnDispatcher().Bind(out sessions).Subscribe();
            
            OutputState = new OutputState(sessionsObservable);

            sessionsSourceList.Add(new SessionState());
            sessionsSourceList.Add(new SessionState());
        }
    }

    public sealed class SessionState : ReactiveObject
    {
        public ProgressState ProgressState { get; }
        public InputState InputState { get; }

        [ObservableAsProperty] public bool IsReadyForExport { get; }

        public  SessionState()
        {
            ProgressState = new ProgressState();
            InputState = new InputState(ProgressState);

            Observable.CombineLatest(
                InputState.WhenAnyValue(x => x.Items, x => x.LDIFolderPath, (items, ldiPath) => (items, ldiPath)),
                ProgressState.WhenAnyValue(
                    x => x.EBOMReportReadProgress, x => x.EBOMReportReadError,
                    x => x.ExistingDataReadProgress, x => x.ExistingDataReadError,
                    x => x.ComparisonProgress,
                    (ebomReportProgress, ebomReportError, existingDataReadProgress, existingDataReadError, comparisonProgress) =>
                        (ebomReportProgress, ebomReportError, existingDataReadProgress, existingDataReadError, comparisonProgress)
                ),

                (inputData, progressData) => inputData.items.Root == null || inputData.items.Root.IsChecked == false ||
                (progressData.ebomReportProgress.IsInExclusiveRange(0, 1) && !progressData.ebomReportError) ||
                (progressData.existingDataReadProgress.IsInExclusiveRange(0, 1) && !progressData.existingDataReadError) ||
                progressData.comparisonProgress.IsInExclusiveRange(0, 1) ||
                !Directory.Exists(inputData.ldiPath) ? false : true
            ).ToPropertyEx(this, x => x.IsReadyForExport);

        }
    }
}
