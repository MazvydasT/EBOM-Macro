using ReactiveUI;

namespace EBOM_Macro.States
{
    public sealed class AppState
    {
        public static AppState State { get; } = new AppState();

        public ProgressState ProgressState { get; }
        public InputState InputState { get; }
        public OutputState OutputState { get; }

        private AppState()
        {
            ProgressState = new ProgressState();
            InputState = new InputState(ProgressState);
            OutputState = new OutputState(InputState, ProgressState);
    }
    }
}
