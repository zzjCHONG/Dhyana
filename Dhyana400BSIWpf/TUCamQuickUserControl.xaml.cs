using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dhyana400BSIWpf
{
    /// <summary>
    /// TUCamQuickUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class TUCamQuickUserControl : UserControl
    {
        public TUCamQuickUserControl()
        {
            InitializeComponent();
            this.DataContext =new TUCamViewModel();
        }

        private void LevelTextBox_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void ResolutionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
