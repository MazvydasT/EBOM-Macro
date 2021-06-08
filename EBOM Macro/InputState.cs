using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using static EBOM_Macro.Item;

namespace EBOM_Macro
{
    public sealed class InputState : ReactiveObject
    {
        public static InputState State { get; } = new InputState();

        [Reactive] public string EBOMReportPath { get; set; }// = @"C:\Users\mtadara1\Desktop\EMS_EBOM_Report_L663_05_05_2021_17_50.csv";
        [Reactive] public string ExistingDataPath { get; set; }

        private InputState()
        {
            var itemsObservable = this.WhenAnyValue(x => x.EBOMReportPath).Select(path => Observable.FromAsync(token => CSVManager2.ReadEBOMReport(path, new Progress<ProgressUpdate>(progress =>
            {
                ProgressState.State.EBOMReportReadProgress = (double)progress.Value / progress.Max;
                ProgressState.State.EBOMReportReadMessage = progress.Message;
            }), token)).Catch((Exception exception) =>
            {
                ProgressState.State.EBOMReportReadMessage = exception.Message;

                return Observable.Return<(Item2, IReadOnlyCollection<Item2>, IReadOnlyCollection<Item2>)>(default);
            })).Switch();

            /*var existingDataObservable = this.WhenAnyValue(x => x.ExistingDataPath).Select(path => Observable.FromAsync(token => CSVManager2.ReadEBOMReport(path, new Progress<ProgressUpdate>(progress =>
            {
                ProgressState.State.EBOMReportReadProgress = (double)progress.Value / progress.Max;
                ProgressState.State.EBOMReportReadMessage = progress.Message;
            }), token)).Catch((Exception exception) =>
            {
                ProgressState.State.EBOMReportReadMessage = exception.Message;

                return Observable.Return<List<Item2>>(null);
            })).Switch();*/
        }
    }
}