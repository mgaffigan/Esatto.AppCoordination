using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICoordinator
    {
        SessionRecord AmbientSession { get; set; }

        IDeploymentCoordinator GetCoordinatorForDeployment(string deployment);
    }
}
