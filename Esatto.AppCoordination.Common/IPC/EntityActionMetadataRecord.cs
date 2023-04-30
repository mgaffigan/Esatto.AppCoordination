using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct EntityActionMetadataRecord
    {
        public Guid Uid;

        public Guid AppUid;

        [MarshalAs(UnmanagedType.BStr)]
        public string Name;

        [MarshalAs(UnmanagedType.BStr)]
        public string Category;

        [MarshalAs(UnmanagedType.BStr)]
        public string ActionTypeName;

        public EntityActionMetadataRecord(Guid uid, Guid appUid,
            string name, string category, string actionTypeName)
        {
            if (!(uid != Guid.Empty))
            {
                throw new ArgumentException("Contract assertion not met: uid != Guid.Empty", nameof(uid));
            }
            if (!(appUid != Guid.Empty))
            {
                throw new ArgumentException("Contract assertion not met: appUid != Guid.Empty", nameof(appUid));
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(name)", nameof(name));
            }
            if (string.IsNullOrEmpty(category))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(category)", nameof(category));
            }

            this.Uid = uid;
            this.AppUid = uid;
            this.Name = name;
            this.Category = category;
            this.ActionTypeName = actionTypeName;
        }

        public EntityActionMetadataRecord(Guid uid, Guid appUid,
            EntityActionMetadata metadata)
            : this(uid, appUid, metadata.Name, metadata.Category, metadata.ActionTypeName)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata), "Contract assertion not met: metadata != null");
            }
        }
    }
}
