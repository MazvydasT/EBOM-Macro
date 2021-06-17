using ReactiveUI;

namespace EBOM_Macro.States
{
    public sealed class AppState// : ReactiveObject
    {
        public static AppState State { get; } = new AppState();

        public ProgressState ProgressState { get; }// = new ProgressState();
        public InputState InputState { get; }// = new InputState(ProgressState);
        public OutputState OutputState { get; }// = new OutputState(InputState, ProgressState);

        private AppState()
        {
            ProgressState = new ProgressState();
            InputState = new InputState(ProgressState);
            OutputState = new OutputState(InputState, ProgressState);
    }
    }
}
