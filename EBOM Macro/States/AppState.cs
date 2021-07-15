using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ChromeTabs;
using DynamicData;
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

        [Reactive] public SessionState SelectedSession { get; set; }

        public ReactiveCommand<Unit, Unit> AddSession { get; }
        public ReactiveCommand<SessionState, Unit> CloseSession { get; }
        public ReactiveCommand<TabReorder, Unit> ReorderSession { get; }

        public OutputState OutputState { get; }

        private AppState()
        {
            SelectedSession = new SessionState();

            sessionsSourceList.Add(SelectedSession);

            var sessionsObservable = sessionsSourceList.Connect().Publish();

            sessionsObservable.ObserveOnDispatcher().Bind(out sessions).Subscribe();

            OutputState = new OutputState(sessionsObservable);

            CloseSession = ReactiveCommand.Create<SessionState>(s => sessionsSourceList.Remove(s));
            
            AddSession = ReactiveCommand.Create(() =>
            {
                var session = new SessionState();
                sessionsSourceList.Add(session);
                SelectedSession = session;
            });

            ReorderSession = ReactiveCommand.Create<TabReorder>(r => sessionsSourceList.Move(r.FromIndex, r.ToIndex));

            sessionsObservable.Connect();
        }
    }
}
