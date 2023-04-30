using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct CoordinatedAppMetadataRecord
    {
        public Guid Uid;

        public CoordinatedAppMetadataRecord(Guid uid)
        {
            if (!(uid != default))
            {
                throw new ArgumentException("Contract assertion not met: uid != default(Guid)", nameof(uid));
            }

            this.Uid = uid;
        }
    }
}
