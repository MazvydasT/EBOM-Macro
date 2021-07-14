using EBOM_Macro.States;
using System.Windows;

namespace EBOM_Macro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Tabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var appState = AppState.State;

            if (Tabs.SelectedIndex == appState.Sessions.Count - 1) appState.AddSession();
        }
    }
}
