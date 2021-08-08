using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EBOM_Macro
{
    /// <summary>
    /// Interaction logic for SessionView.xaml
    /// </summary>
    public partial class SessionView : UserControl
    {
        private GridLength? attributesColumnWidth = null;

        public SessionView()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this)) this.Background = Brushes.Transparent;
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
            attributesColumnWidth = AttributesColumn.Width;

        private void Attributes_Collapsed(object sender, RoutedEventArgs e) =>
            AttributesColumn.Width = new GridLength(0, GridUnitType.Auto);

        private void Attributes_Expanded(object sender, RoutedEventArgs e)
        {
            if (attributesColumnWidth.HasValue)
                AttributesColumn.Width = attributesColumnWidth.Value;

            else
                AttributesColumn.Width = new GridLength(AttributesColumn.MinWidth, GridUnitType.Pixel);
        }
    }
}
