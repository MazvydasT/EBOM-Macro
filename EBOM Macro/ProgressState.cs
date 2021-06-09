using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace EBOM_Macro
{
    public sealed class ProgressState : ReactiveObject
    {
        public static ProgressState State { get; } = new ProgressState();

        [Reactive] public double EBOMReportReadProgress { get; set; }
        [Reactive] public string EBOMReportReadMessage { get; set; }
        
        [Reactive] public double ExistinDataReadProgress { get; set; }
        [Reactive] public string ExistinDataReadMessage { get; set; }

        private ProgressState() { }
    }
}
