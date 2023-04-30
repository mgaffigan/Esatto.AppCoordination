using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Esatto.AppCoordination
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Application.Run(new CoordinatorMessageLoop());
            }
            catch (Exception ex)
            {
                Log.Error($"Exception on coordinator main thread:\r\n{ex}", 1);
            }
        }
    }
}
