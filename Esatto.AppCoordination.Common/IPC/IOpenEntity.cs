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
    public interface IOpenEntity
    {
        EntityIdentityRecord Identity { get; }

        void AddAction(IEntityAction action, EntityActionMetadataRecord metadata);

        void RemoveAction(Guid actionUid);
    }
}
