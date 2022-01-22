using System.Windows;

namespace EBOM_Macro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GridLength? copyFilesRowHeight = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
            copyFilesRowHeight = FileCopyRow.Height;

        private void FileCopyPanel_Collapsed(object sender, RoutedEventArgs e) =>
            FileCopyRow.Height = new GridLength(0, GridUnitType.Auto);

        private void FileCopyPanel_Expanded(object sender, RoutedEventArgs e) =>
            FileCopyRow.Height = copyFilesRowHeight.HasValue ? copyFilesRowHeight.Value :
            new GridLength(0, GridUnitType.Star);
    }
}
