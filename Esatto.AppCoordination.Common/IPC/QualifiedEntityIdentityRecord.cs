using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct QualifiedEntityIdentityRecord
    {
        [MarshalAs(UnmanagedType.BStr)]
        public string Qualifier;

        [MarshalAs(UnmanagedType.BStr)]
        public string Identifier;

        public QualifiedEntityIdentityRecord(string qualifier, string identifier)
        {
            if (String.IsNullOrEmpty(qualifier))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(qualifier)", nameof(qualifier));
            }
            if (String.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(identifier)", nameof(identifier));
            }

            this.Qualifier = qualifier;
            this.Identifier = identifier;
        }

        public QualifiedEntityIdentityRecord(QualifiedEntityIdentity identity)
            : this(identity.Qualifier, identity.Value)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity), "Contract assertion not met: identity != null");
            }
        }
    }
}
