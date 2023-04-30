using Esatto.AppCoordination;
using Esatto.Win32.Com;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Esatto.AppCoordination.SystemHost
{
    class Program
    {
        // no STAThread.  The whole thing is MTA/NTA
        static void Main(string[] args)
        {
            try
            {
                ComInterop.SetAppId(Guid.Parse(ProcessWatcher.OurAppID));

                ServiceBase.Run(new SystemHostService());
            }
            catch (Exception ex)
            {
                Log.Error($"Exception on coordinator main thread:\r\n{ex}", 1);
            }
        }
    }
}
