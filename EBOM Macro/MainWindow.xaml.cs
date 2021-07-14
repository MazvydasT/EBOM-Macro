using EBOM_Macro.States;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        private void TabItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!(e.Source is TabItem tabItem)) return;

            if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed)
                DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.All);
        }

        private void TabItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Source is TabItem tabItemTarget &&
                e.Data.GetData(typeof(TabItem)) is TabItem tabItemSource &&
                !tabItemTarget.Equals(tabItemSource))
            {
                var appState = AppState.State;
                var sessions = appState.Sessions;

                int targetIndex = sessions.IndexOf((SessionState)tabItemTarget.DataContext);

                if(targetIndex == sessions.Count - 1)

                /*tabControl.Items.Remove(tabItemSource);
                tabControl.Items.Insert(targetIndex, tabItemSource);
                tabItemSource.IsSelected = true;*/

                e = e;
            }
        }
    }
}
