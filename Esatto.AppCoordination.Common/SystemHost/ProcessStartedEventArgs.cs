using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.SystemHost
{
    public sealed class ProcessStartedEventArgs : EventArgs
    {
        public Process Process { get; }

        public ProcessStartedEventArgs(int processId)
        {
            this.Process = Process.GetProcessById(processId);
        }
    }
}
