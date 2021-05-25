using PropertyChanged;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace EBOM_Macro
{
    public class AppState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [DoNotNotify]
        public static AppState State { get; } = new AppState();

        private AppState() { }

        public  bool GUIMode { get; } = true;

        public double ProgressValue { get; set; }
        public bool Error { get; set; } = false;
        public string Message { get; set; }

        public string PathToEBOMReport { get; set; }
        public string PathToLDIFolder { get; set; }
        public string Program { get; set; }

        public bool IsPathToEBOMReportValid => (PathToEBOMReport ?? "").Length > 0;
        public bool IsPathToLDIFolderValid => (PathToLDIFolder ?? "").Length > 0;
        public bool IsProgramValid => (Program ?? "").Trim().Length > 0;

        private static readonly SolidColorBrush warningColour = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFB900");
        public Brush PathToEBOMReportBackground => IsPathToEBOMReportValid ? Brushes.White : warningColour;
        public Brush PathToLDIFolderBackground => IsPathToLDIFolderValid ? Brushes.White : warningColour;
        public Brush ProgramBackground => IsProgramValid ? Brushes.White : warningColour;

        public CancellationTokenSource CancellationTokenSource { get; set; }

        public Visibility StartButtonVisibility => CancellationTokenSource == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CancelButtonVisibility => StartButtonVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

        public bool StartButtonIsEnabled => IsPathToEBOMReportValid && IsPathToLDIFolderValid && IsProgramValid;

        public bool InputsEnabled => CancellationTokenSource == null;
    }
}
