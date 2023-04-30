using Esatto.AppCoordination.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    // StandardOleMarshalObject keeps us single-threaded on the UI thread
    // https://msdn.microsoft.com/en-us/library/74169f59(v=vs.110).aspx
    // that does not mean we don't have to deal with Collection Changes during 
    // enumeration since all calls are reentrant.

    // not registered: only instantiated by Coordinator
    [ComVisible(false)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class DeploymentCoordinator : StandardOleMarshalObject, IDeploymentCoordinator
    {
        private readonly string Deployment;
        private readonly ThreadAssert MainThread;

        private readonly Dictionary<Guid, OpenEntityHandle> OpenEntities;
        private readonly Dictionary<Guid, CoordinatedAppProxy> CoordinatedApps;
        private IEnumerable<CoordinatedAppProxy> ConnectedApps => CoordinatedApps.Values.ToArray();

        public DeploymentCoordinator(string deployment)
        {
            this.MainThread = new ThreadAssert();
            this.Deployment = deployment;

            this.OpenEntities = new Dictionary<Guid, OpenEntityHandle>();
            this.CoordinatedApps = new Dictionary<Guid, CoordinatedAppProxy>();
        }

        private class OpenEntityHandle
        {
            public IOpenEntity Entity { get; }
            public readonly EntityIdentityRecord Identity;

            public Guid AppUid => Identity.AppUid;
            public Guid EntityUid => Identity.Uid;

            public OpenEntityHandle(IOpenEntity entity, EntityIdentityRecord identity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity), "Contract assertion not met: entity != null");
                }

                this.Entity = entity;
                this.Identity = identity;
            }
        }

        private class CoordinatedAppProxy
        {
            public ICoordinatedApp CoordinatedApp { get; }
            public CoordinatedAppMetadataRecord Metadata { get; }
            private int FailureCount;
            public bool IsDead { get; private set; }

            public CoordinatedAppProxy(ICoordinatedApp app, CoordinatedAppMetadataRecord metadata)
            {
                if (app == null)
                {
                    throw new ArgumentNullException(nameof(app), "Contract assertion not met: app != null");
                }

                this.CoordinatedApp = app;
                this.Metadata = metadata;
            }

            public void Run(Action action, bool noTrackFailures = false)
            {
                if (IsDead)
                {
                    return;
                }

                try
                {
                    action();
                    FailureCount = 0;
                }
                catch (COMException ex) 
                    when (ex.HResult == unchecked((int)0x80010108) /* RPC_E_DISCONNECTED */
                    || ex.HResult == unchecked((int)0x800706BA) /* RPC_S_SERVER_UNAVAILABLE */)
                {
                    // it's dead, an ex parrot.
                    FailureCount = 999;
                    IsDead = true;
                }
                catch (Exception ex)
                {
                    if (!noTrackFailures)
                    {
                        FailureCount++;
                    }

                    Log.Warn($"Exception when calling back to coordinated app\r\n{ex}", 121);

                    if (FailureCount > 5)
                    {
                        Log.Warn($"{FailureCount} consecutive failures in contacting app {Metadata.Uid}.  Marking as dead\r\n{ex}", 123);
                        IsDead = true;
                    }
                }
            }
        }

        public void NotifyEntityOpened(IOpenEntity entity, EntityIdentityRecord identity)
        {
            MainThread.Assert();
            if (OpenEntities.ContainsKey(identity.Uid))
            {
                throw new InvalidOperationException($"Entity {identity.Uid} already open");
            }
            if (!(entity.Identity.Uid == identity.Uid))
            {
                throw new ArgumentException("Contract assertion not met: entity.Identity.Uid == identity.Uid", nameof(entity));
            }

            PruneApps();
            OpenEntities.Add(identity.Uid, new OpenEntityHandle(entity, identity));

            foreach (var app in ConnectedApps)
            {
                if (app.Metadata.Uid == identity.AppUid)
                {
                    continue;
                }

                app.Run(() => { app.CoordinatedApp.NotifyEntityOpened(entity, identity); });
            }
            // since the call to run may have added dead apps, prune now.
            PruneApps();
        }

        public void NotifyEntityFocused(Guid entityUid)
        {
            MainThread.Assert();

            PruneApps();
            // this just validates that it exists
            var proxy = GetEntityProxyForEntity(entityUid);

            foreach (var app in ConnectedApps)
            {
                if (app.Metadata.Uid == proxy.AppUid)
                {
                    continue;
                }

                app.Run(() => { app.CoordinatedApp.NotifyEntityFocused(entityUid); });
            }
        }

        public void NotifyEntityClosed(Guid entityUid)
        {
            MainThread.Assert();

            PruneApps();
            var proxy = GetEntityProxyForEntity(entityUid);

            NotifyEntityClosedInternal(proxy);
        }

        private void NotifyEntityClosedInternal(OpenEntityHandle proxy)
        {
            OpenEntities.Remove(proxy.EntityUid);

            foreach (var app in ConnectedApps)
            {
                if (app.Metadata.Uid == proxy.AppUid)
                {
                    continue;
                }

                app.Run(() => { app.CoordinatedApp.NotifyEntityClosed(proxy.EntityUid); }, noTrackFailures: true);
            }
        }

        public IOpenEntity[] GetOpenEntities()
        {
            MainThread.Assert();

            return OpenEntities.Values.Select(c => c.Entity).ToArray();
        }

        public void AddOpenEntitiesListener(ICoordinatedApp listener, CoordinatedAppMetadataRecord metadata)
        {
            MainThread.Assert();
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }
            if (!(listener.Metadata.Uid == metadata.Uid))
            {
                throw new ArgumentException("Contract assertion not met: listener.Metadata.Uid == metadata.Uid", nameof(listener));
            }
            if (CoordinatedApps.ContainsKey(metadata.Uid))
            {
                throw new InvalidOperationException($"App {metadata.Uid} is already registered");
            }

            PingAllApps();
            PruneApps();
            var proxy = new CoordinatedAppProxy(listener, metadata);

            foreach (var app in OpenEntities.Values.ToArray())
            {
                if (app.AppUid == proxy.Metadata.Uid)
                {
                    continue;
                }

                listener.NotifyEntityOpened(app.Entity, app.Identity);
            }

            CoordinatedApps.Add(metadata.Uid, proxy);
        }

        private void PingAllApps()
        {
            foreach (var app in ConnectedApps)
            {
                app.Run(() =>
                {
                    // this is a no-op to get an exception if the client is dead
                    app.CoordinatedApp.Metadata.GetType();
                });
            }
        }

        public void RemoveOpenEntitiesListener(Guid appUid)
        {
            MainThread.Assert();
            if (appUid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(appUid));
            }

            CoordinatedAppProxy proxy;
            if (!CoordinatedApps.TryGetValue(appUid, out proxy))
            {
                throw new InvalidOperationException($"App {appUid} is not registered");
            }

            RemoveOpenEntitiesListenerInternal(proxy);
        }

        private void RemoveOpenEntitiesListenerInternal(CoordinatedAppProxy proxy)
        {
            var openApps = OpenEntities.Values.Where(e => e.AppUid == proxy.Metadata.Uid).ToArray();
            if (openApps.Any())
            {
                Log.Warn($"App {proxy.Metadata.Uid} leaked {openApps.Length} windows", 122);
            }
            foreach (var app in openApps)
            {
                NotifyEntityClosedInternal(app);
            }

            CoordinatedApps.Remove(proxy.Metadata.Uid);
        }

        private OpenEntityHandle GetEntityProxyForEntity(Guid entityUid)
        {
            OpenEntityHandle proxy;
            if (!OpenEntities.TryGetValue(entityUid, out proxy))
            {
                throw new InvalidOperationException($"Entity {entityUid} is not open");
            }

            return proxy;
        }

        private void PruneApps()
        {
            var deadApps = CoordinatedApps.Values.Where(p => p.IsDead).ToArray();
            foreach (var deadApp in deadApps)
            {
                RemoveOpenEntitiesListenerInternal(deadApp);
            }
        }
    }
}
