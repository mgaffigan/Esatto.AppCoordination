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
    public interface ICoordinatedApp
    {
        CoordinatedAppMetadataRecord Metadata { get; }

        void NotifyEntityOpened(IOpenEntity entity, EntityIdentityRecord identity);

        void NotifyEntityClosed(Guid entityUid);

        void NotifyEntityFocused(Guid entityUid);
    }
}
