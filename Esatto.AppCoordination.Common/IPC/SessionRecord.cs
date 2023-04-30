using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct SessionRecord
    {
        public string DeploymentName;

        public SessionMetadataTupleRecord[] Metadata;

        public static SessionRecord Default => new SessionRecord();

        public IDictionary<string, string> GetMetadata()
        {
            if (Metadata == null)
            {
                throw new ArgumentNullException(nameof(Metadata));
            }

            var dict = new Dictionary<string, string>();
            foreach (var record in Metadata)
            {
                dict.Add(record.Key, record.Value);
            }
            return dict;
        }

        public SessionRecord(Session session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session), "Contract assertion not met: session != null");
            }

            this.DeploymentName = session.DeploymentName;
            this.Metadata = session.Metadata.Select(kvp => new SessionMetadataTupleRecord(kvp.Key, kvp.Value)).ToArray();
        }

        #region Equality

        public override string ToString() => DeploymentName;
        public override int GetHashCode() => EqualityComparer<string>.Default.GetHashCode(DeploymentName);
        public override bool Equals(object obj) => base.Equals(obj);
        public static bool operator ==(SessionRecord x, SessionRecord y) => x.Equals(y);
        public static bool operator !=(SessionRecord x, SessionRecord y) => !(x == y);

        #endregion
    }
}
