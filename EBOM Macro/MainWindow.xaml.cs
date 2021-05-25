using System;
using System.Windows;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace EBOM_Macro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly AppState state = AppState.State;

        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                state.CancellationTokenSource?.Cancel();
            }
            catch (Exception) { }
        }

        private void UpdateProgress(ProgressUpdate update, double valueMultiplier = 1.0, double offset = 0)
        {
            var completion = offset + (double)update.Value / update.Max * valueMultiplier;

            if (!state.GUIMode)
            {
                Console.Error.Write($"\r{completion:P0}");
            }

            state.Message = update.Message;
            state.ProgressValue = completion;
        }

        private void BrowseEBOMReport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV (Comma delimited) (*.csv)|*.csv",
                RestoreDirectory = true,
                Multiselect = false,
                CheckFileExists = true
            };

            if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                state.PathToEBOMReport = Utils.PathToUNC(dialog.FileName);
            }
        }

        private void BrowseLDIFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                state.PathToLDIFolder = Utils.PathToUNC(dialog.FileName);
            }
        }
    }
}
