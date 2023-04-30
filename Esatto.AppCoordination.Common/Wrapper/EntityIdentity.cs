using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class EntityIdentity : IReadOnlyDictionary<string, string>
    {
        public Guid EntityUid { get; }

        private Dictionary<string, string> _Identifiers;
        public IEnumerable<QualifiedEntityIdentity> Identifiers { get; }

        public string Name { get; }

        public EntityIdentity(string name, IEnumerable<QualifiedEntityIdentity> identifiers)
        {
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

            this.EntityUid = Guid.NewGuid();
            this.Identifiers = new ReadOnlyCollection<QualifiedEntityIdentity>(identifiers.ToArray());
            this._Identifiers = new Dictionary<string, string>();
            foreach (var ident in identifiers)
            {
                _Identifiers[ident.Qualifier] = ident.Value;
            }
            this.Name = name;
        }

        internal EntityIdentity(IPC.EntityIdentityRecord record)
            : this(record.Name, record.Identifiers.Select(r => new QualifiedEntityIdentity(r)))
        {
            this.EntityUid = record.Uid;
        }

        public override string ToString() => Name;

        #region IDictionary implementation

        public IEnumerable<string> Keys => _Identifiers.Keys;

        public IEnumerable<string> Values => _Identifiers.Values;

        public int Count => _Identifiers.Count;

        public string this[string key]
        {
            get
            {
                string result;
                _Identifiers.TryGetValue(key, out result);
                return result;
            }
        }

        public bool ContainsKey(string key) => _Identifiers.ContainsKey(key);

        public bool TryGetValue(string key, out string value) => _Identifiers.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _Identifiers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion IDictionary implementation

        #region Equality

        public static IEqualityComparer<EntityIdentity> KeyValueComparer => new EntityIdentityKeyComparer();

        private sealed class EntityIdentityKeyComparer : IEqualityComparer<EntityIdentity>
        {
            public bool Equals(EntityIdentity x, EntityIdentity y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null)
                {
                    return false;
                }

                if (y == null)
                {
                    return false;
                }
                return x.Identifiers.SequenceEqual(y.Identifiers);
            }

            public int GetHashCode(EntityIdentity obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                int xv = 0;
                foreach (var key in obj.Keys)
                {
                    xv ^= key?.GetHashCode() ?? 0;
                }
                return xv;
            }
        }

        #endregion Equality
    }

    public sealed class EntityIdentityBuilder : IEnumerable<QualifiedEntityIdentity>
    {
        private readonly string Name;

        private readonly List<QualifiedEntityIdentity> Identifiers;

        public EntityIdentityBuilder(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(name)", nameof(name));
            }

            this.Name = name;
            this.Identifiers = new List<QualifiedEntityIdentity>();
        }

        public void Add(string qualifier, string value) => Identifiers.Add(new QualifiedEntityIdentity(qualifier, value));

        public static implicit operator EntityIdentity(EntityIdentityBuilder builder)
        {
            if (builder == null)
            {
                return null;
            }
            return new EntityIdentity(builder.Name, builder.Identifiers);
        }

        #region IEnumerable

        public IEnumerator<QualifiedEntityIdentity> GetEnumerator()
        {
            return ((IEnumerable<QualifiedEntityIdentity>)Identifiers).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<QualifiedEntityIdentity>)Identifiers).GetEnumerator();
        }

        #endregion IEnumerable
    }
}