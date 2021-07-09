using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace EBOM_Macro.States
{
    public class ProgressState : ReactiveObject
    {
        [Reactive] public double EBOMReportReadProgress { get; set; }
        [Reactive] public string EBOMReportReadMessage { get; set; }
        [Reactive] public bool EBOMReportReadError { get; set; }

        [Reactive] public double ExistingDataReadProgress { get; set; }
        [Reactive] public string ExistingDataReadMessage { get; set; }
        [Reactive] public bool ExistingDataReadError { get; set; }

        [Reactive] public double ComparisonProgress { get; set; }
    }
}
