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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    internal partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this.ViewModel = new DemoClientVM("Default");
        }

        public DemoClientVM ViewModel { get; }

        private void btGetSession_Click(object sender, RoutedEventArgs e)
        {
            this.tbSession.Text = ViewModel.GetSession();
        }

        private void btSetSession_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SetSession(this.tbSession.Text);
        }

        private void btAddCommandToSelected_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddActionToSelectedOtherEntity();
        }

        private void btOpenNewEntity_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddMyEntity();
        }

        private void btCloseSelectedEntity_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveSelectedMyEntity();
        }

        private void btClearAllPublishedCommands_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAllPublishedCommands();
        }

        private void btDispose_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Dispose();
        }
    }
}
