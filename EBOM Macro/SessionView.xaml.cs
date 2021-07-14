using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace EBOM_Macro
{
    /// <summary>
    /// Interaction logic for SessionView.xaml
    /// </summary>
    public partial class SessionView : UserControl
    {
        public SessionView()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this)) this.Background = Brushes.Transparent;
        }
    }
}
