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
    // virtual to allow custom implementations
    public class PublishedAction : IDisposable
    {
        private bool isDisposed;
        private CoordinatedApp App;
        private ForeignEntity Entity;
        private PublishedActionProxy Proxy;

        public event EventHandler Invoked;

        private bool IsRegistered;
        private bool IsInitialized => App != null;

        public Guid ActionUid { get; }
        public EntityActionMetadata Metadata { get; private set; }

        public PublishedAction(EntityActionMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata), "Contract assertion not met: metadata != null");
            }

            this.ActionUid = Guid.NewGuid();
            this.Metadata = metadata;
        }

        // time critical, on COM call stack
        internal void NotifyEntityClosed()
        {
            if (!IsInitialized)
            {
                return;
            }
            App.MainThread.Assert();

            IsRegistered = false;
            Dispose();
        }

        internal void Attach(CoordinatedApp app, ForeignEntity entity)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app), "Contract assertion not met: app != null");
            }
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }
            if (IsInitialized)
            {
                throw new InvalidOperationException("Already initialized");
            }
            app.MainThread.Assert();

            this.App = app;
            this.Entity = entity;
            this.Proxy = new PublishedActionProxy(this);
            entity.Entity.AddAction(this.Proxy, this.Proxy.Metadata);
            this.IsRegistered = true;
        }

        protected virtual void OnInvoked()
        {
            Invoked?.Invoke(this, EventArgs.Empty);
        }

        // time critical, on COM call stack
        public void Dispose() => Dispose(true);

        // time critical, on COM call stack
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed || !disposing)
            {
                return;
            }
            isDisposed = true;

            if (!IsInitialized)
            {
                return;
            }
            this.App.MainThread.Assert();

            var entity = this.Entity;
            entity.Actions.RemoveItemInternal(this);
            this.App = null;
            this.Entity = null;
            this.Proxy.Disconnect();

            if (IsRegistered)
            {
                entity.Entity.RemoveAction(ActionUid);
            }
        }

        private class PublishedActionProxy : StandardOleMarshalObject, IEntityAction
        {
            private PublishedAction ThisAction;

            private readonly EntityActionMetadataRecord _Metadata;
            public EntityActionMetadataRecord Metadata => _Metadata;

            public PublishedActionProxy(PublishedAction action)
            {
                if (action == null)
                {
                    throw new ArgumentNullException(nameof(action), "Contract assertion not met: action != null");
                }

                this.ThisAction = action;
                this._Metadata = new EntityActionMetadataRecord(action.ActionUid, action.App.AppUid, action.Metadata);
            }

            public void Invoke()
            {
                if (ThisAction == null)
                {
                    // we have already disconnected
                    Log.Debug($"Callback to PublishedActionProxy.Invoke after disconnect");
                    return;
                }
                ThisAction.App.MainThread.Assert();

                ThisAction.OnInvoked();
            }

            // Since other applications may have references on this object, we loose our references so that
            // we don't keep the published entity alive unneccessarily.
            // time critical, on COM call stack
            internal void Disconnect()
            {
                if (ThisAction == null)
                {
                    throw new InvalidOperationException("Already disconnected");
                }

                ThisAction = null;
            }
        }
    }
}
