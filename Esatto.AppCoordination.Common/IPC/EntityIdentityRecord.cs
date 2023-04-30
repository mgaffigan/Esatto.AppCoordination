using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct EntityIdentityRecord
    {
        public Guid Uid;

        public Guid AppUid;

        public QualifiedEntityIdentityRecord[] Identifiers;

        [MarshalAs(UnmanagedType.BStr)]
        public string Name;

        public EntityIdentityRecord(Guid uid, Guid appUid, string name, QualifiedEntityIdentityRecord[] identifiers)
        {
            if (!(uid != default))
            {
                throw new ArgumentException("Contract assertion not met: uid != default(Guid)", nameof(uid));
            }
            if (!(appUid != default))
            {
                throw new ArgumentException("Contract assertion not met: appUid != default(Guid)", nameof(appUid));
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(name)", nameof(name));
            }
            if (identifiers == null)
            {
                throw new ArgumentNullException(nameof(identifiers), "Contract assertion not met: identifiers != null");
            }
            if (!(identifiers.Any()))
            {
                throw new ArgumentException("Contract assertion not met: identifiers.Any()", nameof(identifiers));
            }

            this.Uid = uid;
            this.AppUid = appUid;
            this.Name = name;
            this.Identifiers = identifiers;
        }

        public EntityIdentityRecord(EntityIdentity identity, Guid appUid)
            : this(identity.EntityUid, appUid, identity.Name, 
                  identity.Identifiers.Select(s => new QualifiedEntityIdentityRecord(s)).ToArray())
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity), "Contract assertion not met: identity != null");
            }
        }

        #region Equality

        public override string ToString() => Uid.ToString();

        public override bool Equals(object obj)
        {
            if (!(obj is EntityIdentityRecord))
            {
                return false;
            }

            return ((EntityIdentityRecord)obj).Uid == this.Uid;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(EntityIdentityRecord op1, EntityIdentityRecord op2)
        {
            return op1.Equals(op2);
        }

        public static bool operator !=(EntityIdentityRecord op1, EntityIdentityRecord op2)
        {
            return !op1.Equals(op2);
        }

        #endregion
    }
}
