using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Esatto.AppCoordination.DemoClient
{
    internal static class Program
    {
        public static Guid AppId = Guid.NewGuid();

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var app = new App();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception while running demo client\r\n{ex}", "Demo Client");
            }
        }
    }
}
