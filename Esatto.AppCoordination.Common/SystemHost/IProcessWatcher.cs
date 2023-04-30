using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.SystemHost
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IProcessWatcher
    {
        void StartWatchingForProcess(int sessionId, string processName, IProcessWatcherClient client);

        void Disconnect(IProcessWatcherClient client);
    }
}
