using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class EntityActionMetadata
    {
        public string Name { get; }

        public string Category { get; }

        public string ActionTypeName { get; }

        public EntityActionMetadata(string name, string category, string actionTypeName)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(name)", nameof(name));
            }
            if (String.IsNullOrEmpty(category))
            {
                throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(category)", nameof(category));
            }

            this.Name = name;
            this.Category = category;
            this.ActionTypeName = actionTypeName;
        }
    }
}
