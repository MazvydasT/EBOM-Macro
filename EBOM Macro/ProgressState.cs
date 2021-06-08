using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EBOM_Macro
{
    public sealed class ProgressState : ReactiveObject
    {
        public static ProgressState State { get; } = new ProgressState();

        [Reactive] public double EBOMReportReadProgress { get; set; }
        [Reactive] public string EBOMReportReadMessage { get; set; }

        private ProgressState() { }
    }
}
