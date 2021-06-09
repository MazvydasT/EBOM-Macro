using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace EBOM_Macro
{
    public sealed class InputState : ReactiveObject
    {
        public static InputState State { get; } = new InputState();

        [Reactive] public string EBOMReportPath { get; set; } // = @"C:\Users\mtadara1\Desktop\EMS_EBOM_Report_L663_05_05_2021_17_50.csv";
        [Reactive] public string ExistingDataPath { get; set; }
        [Reactive] public string LDIPath { get; set; }
        [Reactive] public string ExternalIdPrefix { get; set; }

        private InputState()
        {
            var itemsObservable = this.WhenAnyValue(x => x.EBOMReportPath).Select(path => Observable.FromAsync(token => CSVManager2.ReadEBOMReport(
                path,
                new Progress<ProgressUpdate>(progress =>
            {
                ProgressState.State.EBOMReportReadProgress = (double)progress.Value / progress.Max;
                ProgressState.State.EBOMReportReadMessage = progress.Message;
            }), token)).Catch((Exception exception) =>
            {
                ProgressState.State.EBOMReportReadMessage = exception.Message;

                return default;
            })).Switch();

            var existingDataObservable = this.WhenAnyValue(x => x.ExistingDataPath).Select(path => Observable.FromAsync(token => CSVManager2.ReadDSList(path, new Progress<ProgressUpdate>(progress =>
            {
                ProgressState.State.ExistinDataReadProgress = (double)progress.Value / progress.Max;
                ProgressState.State.ExistinDataReadMessage = progress.Message;
            }), token)).Catch((Exception exception) =>
            {
                ProgressState.State.ExistinDataReadMessage = exception.Message;

                return default;
            })).Switch();

            var externalIdPrefixObservable = this.WhenAnyValue(x => x.ExternalIdPrefix).Select(prefix => prefix?.Trim().ToUpper() ?? "");

            Observable.CombineLatest(itemsObservable, externalIdPrefixObservable, (items, externalIdPrefix) =>
            {
                if (items.Root == null) return items;

                

                return items;
            });
        }
    }
}