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
    public interface IDeploymentCoordinator
    {
        void NotifyEntityOpened(IOpenEntity entity, EntityIdentityRecord identity);

        void NotifyEntityFocused(Guid entityUid);

        void NotifyEntityClosed(Guid entityUid);

        IOpenEntity[] GetOpenEntities();

        void AddOpenEntitiesListener(ICoordinatedApp listener, CoordinatedAppMetadataRecord metadata);

        void RemoveOpenEntitiesListener(Guid appUid);
    }
}
