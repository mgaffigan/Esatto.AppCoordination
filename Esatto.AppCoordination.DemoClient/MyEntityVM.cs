using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.DemoClient
{
    class MyEntityVM : PublishedEntity
    {
        //public new ObservableCollection<MyEntityActionVM> Actions { get; }

        public MyEntityVM(EntityIdentity identity)
            : base(identity)
        {
            //this.Actions = new FilteredObservableCollection<ForeignAction, MyEntityActionVM>(
            //    base.Actions, transform: fa => new MyEntityActionVM(fa));
        }
    }
}
