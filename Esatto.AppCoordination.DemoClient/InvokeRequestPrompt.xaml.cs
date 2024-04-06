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

namespace Esatto.AppCoordination.DemoClient
{
    /// <summary>
    /// Interaction logic for InvokeRequestPrompt.xaml
    /// </summary>
    public partial class InvokeRequestPrompt : Window
    {
        private readonly ForeignEntry Entry;

        public InvokeRequestPrompt(ForeignEntry entry)
        {
            InitializeComponent();

            this.Entry = entry;
            this.Title = $"Invoke {entry.Key}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show($"Result: '{Entry.Invoke(tbPayload.Text)}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            DialogResult = true;
        }
    }
}
