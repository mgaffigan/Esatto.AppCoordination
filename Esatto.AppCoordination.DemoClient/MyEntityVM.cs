using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.DemoClient
{
    internal class MyEntityVM
    {
        public MyEntityVM(PublishedEntry entry, CoordinatedApp app)
        {
            this.Entry = entry;
            this.Commands = app.ForeignEntities.CreateProjection(CPath.Suffix(entry.Key, "command"));
        }

        public PublishedEntry Entry { get; }
        public FilteredForeignEntryCollection Commands { get; }
    }
}
