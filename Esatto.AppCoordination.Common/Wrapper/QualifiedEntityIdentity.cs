using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class QualifiedEntityIdentity : IEquatable<QualifiedEntityIdentity>
    {
        public string Qualifier { get; }

        public string Value { get; }

        public QualifiedEntityIdentity(string qualifier, string identifier)
        {
            if (string.IsNullOrEmpty(qualifier))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(qualifier)", nameof(qualifier));
            }
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(identifier)", nameof(identifier));
            }

            this.Qualifier = qualifier;
            this.Value = identifier;
        }

        internal QualifiedEntityIdentity(IPC.QualifiedEntityIdentityRecord record)
            : this(record.Qualifier, record.Identifier)
        {
        }

        public override string ToString() => $"{Qualifier}: '{Value}'";

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Qualifier.GetHashCode();
                hash = hash * 23 + Value.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj) => Equals(obj as QualifiedEntityIdentity);

        public bool Equals(QualifiedEntityIdentity other)
        {
            return this.Qualifier == other.Qualifier
                && this.Value == other.Value;
        }
    }
}
