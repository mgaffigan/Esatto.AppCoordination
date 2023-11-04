using Esatto.AppCoordination;
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

namespace Esatto.AppCoordination.DemoClient
{
    /// <summary>
    /// Interaction logic for MyEntityActionView.xaml
    /// </summary>
    internal partial class MyEntityActionView : UserControl
    {
        public MyEntityActionView()
        {
            InitializeComponent();
        }

        public ForeignEntry ViewModel => (ForeignEntry)DataContext;

        private void btAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.Invoke("test");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
