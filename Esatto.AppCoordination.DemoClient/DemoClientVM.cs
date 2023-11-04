using Esatto.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.DemoClient
{
    internal class DemoClientVM : NotificationObject, IDisposable
    {
        private readonly CoordinatedApp ThisApp;

        private OpenEntityVM _SelectedOtherEntity;
        public OpenEntityVM SelectedOtherEntity
        {
            get { return _SelectedOtherEntity; }
            set
            {
                _SelectedOtherEntity = value;

                RaisePropertyChanged(nameof(SelectedOtherEntity));
                RaisePropertyChanged(nameof(CanAddActionToSelectedOtherEntity));
            }
        }
        public bool CanAddActionToSelectedOtherEntity => SelectedOtherEntity != null;
        public IObservableCollection<OpenEntityVM> OpenEntities { get; }

        private MyEntityVM _SelectedMyEntity;
        public MyEntityVM SelectedMyEntity
        {
            get { return _SelectedMyEntity; }
            set
            {
                _SelectedMyEntity = value;

                if (value is not null)
                {
                    var props = value.Entry.Value.Clone();
                    props["poke"] = 1 + (int)props["poke"];
                    value.Entry.Value = props;
                }

                RaisePropertyChanged(nameof(SelectedMyEntity));
                RaisePropertyChanged(nameof(CanRemoveSelectedEntity));
            }
        }
        public bool CanRemoveSelectedEntity => SelectedMyEntity != null;
        public ObservableCollection<MyEntityVM> MyEntities { get; }

        public DemoClientVM(ILogger<DemoClientVM> logger)
        {
            this.ThisApp = new CoordinatedApp(SynchronizationContext.Current, silentlyFail: false, logger);
            this.OpenEntities = new ObservableCollectionProxy<ForeignEntry>(ThisApp.ForeignEntities)
                .SelectObservable(fe => new OpenEntityVM(fe, ThisApp));
            this.MyEntities = new ObservableCollection<MyEntityVM>();
            this.entityNumber = Process.GetCurrentProcess().Id * 100;
        }

        private int entityNumber;

        public void AddMyEntity()
        {
            var ent = ThisApp.Publish(CPath.From("Entity", $"Example {entityNumber++}"), new()
            {
                { "DisplayName", $"Example {entityNumber}" },
                { "example", "value" },
                { "poke", 1 },
                { "entityNumber", entityNumber }
            });
            this.MyEntities.Add(new MyEntityVM(ent, ThisApp));
        }

        public void RemoveSelectedMyEntity()
        {
            var myEntity = SelectedMyEntity;
            if (myEntity == null)
            {
                throw new InvalidOperationException("No item selected");
            }

            MyEntities.Remove(myEntity);
            myEntity.Entry.Dispose();
        }

        private int actionId;
        public void AddActionToSelectedOtherEntity()
        {
            var entity = SelectedOtherEntity;
            if (entity == null)
            {
                throw new InvalidOperationException("No entity selected");
            }

            entity.AddAction($"Action {actionId++}");
        }

        public void ClearAllPublishedCommands()
        {
            foreach (var entity in OpenEntities)
            {
                entity.ClearActions();
            }
        }

        public void Dispose()
        {
            ThisApp.Dispose();
        }
    }
}
