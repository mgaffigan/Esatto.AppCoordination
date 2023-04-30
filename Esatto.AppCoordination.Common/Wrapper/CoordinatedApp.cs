using Esatto.AppCoordination.IPC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    // unsealed to allow other access to IDeploymentCoordinator
    public class CoordinatedApp : INotifyPropertyChanged, IDisposable
    {
        internal readonly ThreadAssert MainThread;
        internal protected IDeploymentCoordinator Coordinator { get; private set; }
        private readonly CoordinatedAppProxy AppProxy;
        public Guid AppUid { get; private set; }
        private bool isDisposed;
        public SynchronizationContext CallbackSynchronizationContext { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public ForeignEntityCollection ForeignEntities { get; }
        public PublishedEntityCollection PublishedEntities { get; }

        public bool IsFaulted { get; private set; }

        public CoordinatedApp(IDeploymentCoordinator coordinator, SynchronizationContext callbackSyncCtx)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator), "Contract assertion not met: coordinator != null");
            }
            if (!(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA))
            {
                throw new ArgumentException("Contract assertion not met: Thread.CurrentThread.GetApartmentState() == ApartmentState.STA", "value");
            }

            this.MainThread = new ThreadAssert();
            this.Coordinator = coordinator;
            this.CallbackSynchronizationContext = callbackSyncCtx ?? SynchronizationContext.Current ?? new SynchronizationContext();

            this.AppUid = Guid.NewGuid();
            this.AppProxy = new CoordinatedAppProxy(this);

            this.ForeignEntities = new ForeignEntityCollection(this);
            this.PublishedEntities = new PublishedEntityCollection(this);

            this.Coordinator.AddOpenEntitiesListener(AppProxy, AppProxy.Metadata);
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;

            foreach (var window in PublishedEntities.ToArray())
            {
                window.Dispose();
            }

            var publishedActions = ForeignEntities.SelectMany(f => f.Actions).ToArray();
            foreach (var action in publishedActions)
            {
                action.Dispose();
            }

            this.Coordinator.RemoveOpenEntitiesListener(AppUid);
        }

        private sealed class CoordinatedAppProxy : StandardOleMarshalObject, ICoordinatedApp
        {
            private readonly CoordinatedApp Parent;

            private CoordinatedAppMetadataRecord _Metadata;
            public CoordinatedAppMetadataRecord Metadata => _Metadata;

            public CoordinatedAppProxy(CoordinatedApp parent)
            {
                if (parent == null)
                {
                    throw new ArgumentNullException(nameof(parent), "Contract assertion not met: parent != null");
                }

                this.Parent = parent;
                this._Metadata = new CoordinatedAppMetadataRecord(Parent.AppUid);
            }

            // time critical, on COM call stack
            public void NotifyEntityOpened(IOpenEntity entity, EntityIdentityRecord identity)
            {
                Parent.MainThread.Assert();
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }

                var vm = Parent.ForeignEntities[identity.Uid];
                if (vm != null)
                {
                    throw new ArgumentException($"Entity '{identity}' is already open", nameof(entity));
                }

                Parent.ForeignEntities.Add(new ForeignEntity(entity, identity, Parent));
            }

            // time critical, on COM call stack
            public void NotifyEntityFocused(Guid entityUid)
            {
                if (entityUid == Guid.Empty)
                {
                    throw new ArgumentNullException(nameof(entityUid));
                }
                Parent.MainThread.Assert();

                var vm = GetVMForEntity(entityUid);
                Parent.ForeignEntities.Focus(vm);
            }

            // time critical, on COM call stack
            public void NotifyEntityClosed(Guid entityUid)
            {
                if (entityUid == Guid.Empty)
                {
                    throw new ArgumentNullException(nameof(entityUid));
                }
                Parent.MainThread.Assert();

                var vm = GetVMForEntity(entityUid);
                Parent.ForeignEntities.Remove(vm);
            }

            // time critical, on COM call stack
            private ForeignEntity GetVMForEntity(Guid entityUid)
            {
                if (entityUid == default)
                {
                    throw new ArgumentNullException(nameof(entityUid));
                }

                var vm = Parent.ForeignEntities[entityUid];
                if (vm == null)
                {
                    throw new ArgumentException($"Entity '{entityUid}' is not open", nameof(entityUid));
                }

                return vm;
            }
        }

        // time critical, on COM call stack
        internal void DispatchCallback(Action action)
        {
            CallbackSynchronizationContext.Post(_1 => action(), null);
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Contract assertion not met: !string.IsNullOrEmpty(propertyName)", nameof(propertyName));
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
