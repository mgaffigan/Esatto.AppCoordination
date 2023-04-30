using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class Session
    {
        public Guid EntityUid { get; }

        public ReadOnlyDictionary<string, string> Metadata { get; }

        public string DeploymentName { get; }

        public Session(string deploymentName, IDictionary<string, string> identifiers)
        {
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(deploymentName)", nameof(deploymentName));
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
            this.Metadata = new ReadOnlyDictionary<string, string>(identifiers);
            this.DeploymentName = deploymentName;
        }

        internal Session(IPC.SessionRecord record)
            : this(record.DeploymentName, record.GetMetadata())
        {
        }
    }

    public sealed class SessionBuilder : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly string Name;

        private readonly Dictionary<string, string> Identifiers;

        public SessionBuilder(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(name)", nameof(name));
            }

            this.Name = name;
            this.Identifiers = new Dictionary<string, string>();
        }

        public void Add(string key, string value) => Identifiers.Add(key, value);

        public static implicit operator Session(SessionBuilder builder) => new Session(builder?.Name, builder?.Identifiers);

        #region IEnumerable

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)Identifiers).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)Identifiers).GetEnumerator();
        }

        #endregion
    }
}
