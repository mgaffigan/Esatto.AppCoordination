using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public sealed class ForeignAction
    {
        private readonly PublishedEntity Entity;
        private readonly CoordinatedApp App;
        private readonly EntityActionMetadataRecord Metadata;

        private ThreadAssert MainThread => App.MainThread;

        public Guid ActionUid => Metadata.Uid;

        public string Name => Metadata.Name;
        public string Category => Metadata.Category;
        public string ActionTypeName => Metadata.ActionTypeName;

        public IEntityAction RawAction { get; }

        public event EventHandler Closed;

        // time critical, on COM call stack
        internal ForeignAction(PublishedEntity entity, CoordinatedApp app, EntityActionMetadataRecord metadata, IEntityAction action)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
            }
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app), "Contract assertion not met: app != null");
            }
            if (!(metadata.Uid != Guid.Empty))
            {
                throw new ArgumentException("Contract assertion not met: metadata.Uid != Guid.Empty", nameof(metadata));
            }

            this.Entity = entity;
            this.App = app;
            this.Metadata = metadata;

            this.RawAction = action;
        }

        public void Invoke()
        {
            try
            {
                ComInterop.CoAllowSetForegroundWindow(RawAction);
            }
            catch (Exception ex)
            {
                Log.Debug($"CoAllowSetForegroundWindow failed\r\n{ex}");
            }

            RawAction.Invoke();
        }

        // on DispatchCallback already
        internal void NotifyClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
