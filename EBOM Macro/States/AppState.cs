using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
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

        [Reactive] public int SelectedIndex { get; set; } = 0;
        
        public OutputState OutputState { get; }

        private AppState()
        {
            AddSession();
            AddSession();

            var sessionsObservable = sessionsSourceList.Connect().Publish();

            sessionsObservable.ObserveOnDispatcher().Bind(out sessions).Subscribe();

            OutputState = new OutputState(sessionsObservable);

            sessionsObservable.Connect();
        }

        public void AddSession() => sessionsSourceList.Add(new SessionState());

        public void CloseSession(SessionState session)
        {
            --SelectedIndex;
            sessionsSourceList.Remove(session);
        }

        public void SetSessionIndex(SessionState session, int index) => sessionsSourceList.Move(sessionsSourceList.Items.IndexOf(session), index);
    }
}
