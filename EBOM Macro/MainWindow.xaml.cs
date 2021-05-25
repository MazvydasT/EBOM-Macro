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

            /*if (state.GUIMode)
            {
                InitializeComponent();

                this.DataContext = state;
            }

            else
            {
                throw new NotImplementedException();
                Convert(null).ContinueWith(_ => this.Dispatcher.Invoke(() => Application.Current.Shutdown()));
            }*/
        }

        /*private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                RestoreDirectory = true,
                OverwritePrompt = true,
                Filter = "eM-Planner data (*.xml)|*.xml"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await Convert(dialog.FileName);
            }
        }*/
        
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

        /*private async Task Convert(string outputPath)
        {
            //using (var streamReader = new StreamReader(@"C:\Users\mtadara1\Desktop\EMS_EBOM_Report_L663_05_05_2021_17_50.csv"))
            //using (var streamReader = new StreamReader(@"\\gal7gbi2017prod.fs15.util.jlrint.com\PLM_2.0\P2\EBOM_Report\05-05-2021\EMS_EBOM_Report_L663_05_05_2021_17_50.csv"))

            if (!state.GUIMode)
            {
                Console.Clear();
            }

            state.Message = null;
            state.ProgressValue = 0;

            using (state.CancellationTokenSource = new CancellationTokenSource())
            {
                try
                {
                    var items = await EBOMReportManager.GetItems(
                        //@"C:\Users\mtadara1\Desktop\EMS_EBOM_Report_L663_05_05_2021_17_50.csv",
                        //@"C:\Users\mtadara1\Desktop\DS-L663-060301-C01-A1.csv",
                        state.PathToEBOMReport,

                        //"L663",
                        state.Program,

                        new Progress<ProgressUpdate>(update => UpdateProgress(update, 0.5)),

                        state.CancellationTokenSource.Token
                    );

                    using (var fileStream = new FileStream(
                        //@"C:\Users\mtadara1\Desktop\output.xml",
                        //@"C:\Users\mtadara1\Desktop\DS-L663-060301-C01-A1.xml",
                        outputPath,

                        FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)
                    )
                    using (var streamWriter = new StreamWriter(fileStream))
                    using (var xmlWriter = XmlWriter.Create(streamWriter, new XmlWriterSettings()
                    {
                        Indent = true,
                        NewLineChars = "\n",
                        IndentChars = "\t"
                    }))
                    {
                        await EBOMReportManager.ItemsToXML(
                            xmlWriter,

                            items,

                            //@"Z:\L663\LDI",
                            state.PathToLDIFolder,

                            new Progress<ProgressUpdate>(update => UpdateProgress(update, 0.5, 0.5)),

                            state.CancellationTokenSource.Token
                        );

                        fileStream.SetLength(fileStream.Position); // Trunkates existing file
                    }

                    var done = "Done";

                    if (!state.GUIMode)
                    {
                        Console.Error.WriteLine($"\n{done}");
                    }

                    state.Message = done;
                }

                catch (OperationCanceledException operationCancelledException)
                {
                    if (!state.GUIMode)
                    {
                        Console.Error.WriteLine($"\n{operationCancelledException.Message}");
                    }

                    state.ProgressValue = 0;
                    state.Message = null;
                }

                catch(Exception exception)
                {
                    state.Error = true;
                    state.Message = exception.Message;
                }
            }

            state.CancellationTokenSource = null;
        }*/

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
