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
using System.Windows.Shapes;

namespace sgbono_windows_update
{
    /// <summary>
    /// Interaction logic for WarningWindow.xaml
    /// </summary>
    public partial class WarningWindow : Window
    {

        public WarningWindow()
        {
            InitializeComponent();
            this.Topmost = true;
        }

        private void termsCheckbox_Click(object sender, RoutedEventArgs e)
        {
            submitButton.IsEnabled = termsCheckbox.IsChecked ?? false;
        }

        private void SubmitCancelClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
