using Esatto.AppCoordination.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    internal sealed class NewDeploymentCoordinatorEventArgs : EventArgs
    {
        public IDeploymentCoordinator Coordinator { get; }

        public NewDeploymentCoordinatorEventArgs(IDeploymentCoordinator coordinator)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator), "Contract assertion not met: coordinator != null");
            }

            this.Coordinator = coordinator;
        }
    }
}
