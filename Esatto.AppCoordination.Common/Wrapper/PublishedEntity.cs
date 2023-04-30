using Esatto.AppCoordination.IPC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    // virtual to allow subclass by implementing applications
    public class PublishedEntity : IDisposable, INotifyPropertyChanged
    {
        public EntityIdentity Identity { get; }
        public Guid EntityUid => Identity.EntityUid;
        public ForeignActionCollection Actions { get; private set; }

        private bool isDisposed;
        private CoordinatedApp Parent;
        private CoordinatedEntityProxy Proxy;

        public event PropertyChangedEventHandler PropertyChanged;

        private bool IsInitialized => Parent != null;

        public PublishedEntity(EntityIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity), "Contract assertion not met: identity != null");
            }

            this.Identity = identity;
        }

        internal void Attach(CoordinatedApp parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent), "Contract assertion not met: parent != null");
            }
            if (IsInitialized)
            {
                throw new InvalidOperationException("Already initialized");
            }
            parent.MainThread.Assert();

            this.Parent = parent;
            this.Actions = new ForeignActionCollection(this, parent);
            this.Proxy = new CoordinatedEntityProxy(this);
            parent.Coordinator.NotifyEntityOpened(Proxy, Proxy.Identity);

            RaisePropertyChanged(nameof(Actions));
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;

            if (!IsInitialized)
            {
                return;
            }
            this.Parent.MainThread.Assert();

            Parent.PublishedEntities.RemoveItemInternal(this);

            var parent = Parent;
            this.Parent = null;
            Proxy.Disconnect();
            parent.Coordinator.NotifyEntityClosed(EntityUid);
        }

        public void Focus()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Not initialized");
            }
            Parent.MainThread.Assert();

            Parent.Coordinator.NotifyEntityFocused(EntityUid);
        }

        private sealed class CoordinatedEntityProxy : StandardOleMarshalObject, IOpenEntity
        {
            private PublishedEntity ThisEntity;

            // time critical, on COM call stack
            public EntityIdentityRecord Identity { get; }

            public CoordinatedEntityProxy(PublishedEntity thisEntity)
            {
                if (thisEntity == null)
                {
                    throw new ArgumentNullException(nameof(thisEntity), "Contract assertion not met: thisEntity != null");
                }

                this.ThisEntity = thisEntity;
                this.Identity = new EntityIdentityRecord(thisEntity.Identity, thisEntity.Parent.AppUid);
            }

            // time critical, on COM call stack
            public void AddAction(IEntityAction action, EntityActionMetadataRecord metadata)
            {
                if (action == null)
                {
                    throw new ArgumentNullException(nameof(action));
                }
                if (metadata.Uid == default)
                {
                    throw new ArgumentNullException(nameof(metadata));
                }
                if (ThisEntity == null)
                {
                    // we have already disconnected
                    Log.Debug($"Callback to AddAction after disconnect");
                    return;
                }
                ThisEntity.Parent.MainThread.Assert();

                var vm = ThisEntity.Actions[metadata.Uid];
                if (vm != null)
                {
                    throw new InvalidOperationException($"Action {metadata.Uid} is already registered");
                }

                ThisEntity.Actions.Add(new ForeignAction(ThisEntity, ThisEntity.Parent, metadata, action));
            }

            // time critical, on COM call stack
            public void RemoveAction(Guid actionUid)
            {
                if (actionUid == default)
                {
                    throw new ArgumentNullException(nameof(actionUid));
                }
                if (ThisEntity == null)
                {
                    // we have already disconnected
                    Log.Debug($"Callback to RemoveAction after disconnect");
                    return;
                }

                ThisEntity.Parent.MainThread.Assert();

                var vm = ThisEntity.Actions[actionUid];
                if (vm == null)
                {
                    throw new InvalidOperationException($"Action {actionUid} is not registered");
                }

                ThisEntity.Actions.Remove(vm);
            }

            // Since other applications may have references on this object, we loose our references so that
            // we don't keep the published entity alive unneccessarily.
            public void Disconnect()
            {
                if (ThisEntity == null)
                {
                    throw new InvalidOperationException("Already disconnected");
                }

                ThisEntity = null;
            }
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
