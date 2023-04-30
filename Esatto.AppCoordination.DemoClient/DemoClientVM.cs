using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.DemoClient
{
    internal class DemoClientVM : NotificationObject, IDisposable
    {
        private const string DemoClientMetadataKey = "602BEF41-CF1F-4868-A377-6CF32E8B8FD0";
        private readonly SystemCoordinator SysCoordinator;
        private readonly string DeploymentName;
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
        public ObservableCollection<OpenEntityVM> OpenEntities { get; }

        private MyEntityVM _SelectedMyEntity;
        public MyEntityVM SelectedMyEntity
        {
            get { return _SelectedMyEntity; }
            set
            {
                _SelectedMyEntity = value;

                value?.Focus();

                RaisePropertyChanged(nameof(SelectedMyEntity));
                RaisePropertyChanged(nameof(CanRemoveSelectedEntity));
            }
        }
        public bool CanRemoveSelectedEntity => SelectedMyEntity != null;
        public ObservableCollection<MyEntityVM> MyEntities { get; }

        public DemoClientVM(string deploymentName)
        {
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(deploymentName)", nameof(deploymentName));
            }

            this.DeploymentName = deploymentName;

            this.OpenEntities = new ObservableCollection<OpenEntityVM>();
            this.MyEntities = new ObservableCollection<MyEntityVM>();

            this.SysCoordinator = SystemCoordinator.GetCurrent();
            this.entityNumber = Process.GetCurrentProcess().Id;
            this.ThisApp = SysCoordinator.CreateAppForDeployment(deploymentName);
            this.ThisApp.ForeignEntities.CollectionChanged += ForeignEntities_CollectionChanged;
        }

        private void ForeignEntities_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                this.OpenEntities.Add(new OpenEntityVM((ForeignEntity)e.NewItems[0]));
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                var oe = OpenEntities.Single(o => o.Entity == e.OldItems[0]);
                OpenEntities.Remove(oe);
            }
            else throw new NotSupportedException();
        }

        public string GetSession()
        {
            return SysCoordinator.AmbientSession?.Metadata[DemoClientMetadataKey];
        }

        public void SetSession(string value)
        {
            SysCoordinator.AmbientSession = new SessionBuilder(DeploymentName)
            {
                { DemoClientMetadataKey, value }
            };
        }

        private int entityNumber;

        public void AddMyEntity()
        {
            var mev = new MyEntityVM(new EntityIdentityBuilder($"Entity {entityNumber++}")
            {
                { "entityId", Guid.NewGuid().ToString() }
            });
            this.MyEntities.Add(mev);
            this.ThisApp.PublishedEntities.Add(mev);
        }

        public void RemoveSelectedMyEntity()
        {
            var myEntity = SelectedMyEntity;
            if (myEntity == null)
            {
                throw new InvalidOperationException("No item selected");
            }

            MyEntities.Remove(myEntity);
            myEntity.Dispose();
        }

        private int actionid;
        public void AddActionToSelectedOtherEntity()
        {
            var entity = SelectedOtherEntity;
            if (entity == null)
            {
                throw new InvalidOperationException("No entity selected");
            }

            entity.AddAction($"Action {actionid++}");
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
