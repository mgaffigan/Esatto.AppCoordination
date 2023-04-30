using Esatto.AppCoordination.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.Extensibility
{
    public interface ICoordinatorHook : IDisposable
    {
        void Initialize(ICoordinator coordinator);

        void NotifyNewDeployment(IDeploymentCoordinator coordinator);
    }
}
