using ReactiveUI;

namespace EBOM_Macro.States
{
    public sealed class AppState : ReactiveObject
    {
        public static AppState State { get; } = new AppState();

        public static ProgressState ProgressState { get; } = new ProgressState();
        public static InputState InputState { get; } = new InputState(ProgressState);
        public static OutputState OutputState { get; } = new OutputState(InputState, ProgressState);
    }
}
