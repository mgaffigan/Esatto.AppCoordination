using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using System;
using System.Threading;

namespace Esatto.AppCoordination
{
    public sealed class SystemCoordinator
    {
        private ICoordinator Coordinator;

        private SystemCoordinator(ICoordinator coord)
        {
            if (coord == null)
            {
                throw new ArgumentNullException(nameof(coord), "Contract assertion not met: coord != null");
            }

            this.Coordinator = coord;
        }

        public static SystemCoordinator GetCurrent() 
            => new SystemCoordinator(ComInterop.CreateLocalServer<ICoordinator>(IpcConstants.CoordinatorProgID));

        public Session AmbientSession
        {
            get
            {
                var sessRecord = Coordinator.AmbientSession;
                return sessRecord.DeploymentName == null ? null : new Session(sessRecord);
            }
            set
            {
                Coordinator.AmbientSession = value == null ? default : new SessionRecord(value);
            }
        }

        public CoordinatedApp CreateAppForDeployment(string deploymentName, SynchronizationContext callbackCtx = null)
        {
            var deploymentCoordinator = Coordinator.GetCoordinatorForDeployment(deploymentName);
            if (deploymentCoordinator == null)
            {
                throw new ArgumentException("Contract assertion not met: deploymentCoordinator != null", "value");
            }

            return new CoordinatedApp(deploymentCoordinator, callbackCtx);
        }
    }
}
