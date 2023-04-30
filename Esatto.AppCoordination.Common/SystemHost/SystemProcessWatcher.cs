using Esatto.Win32.Com;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.SystemHost
{
    public sealed class SystemProcessWatcher : IDisposable
    {
        private readonly int SessionID;
        private IProcessWatcher Server;
        private readonly CallbackThunk Client;
        private readonly SynchronizationContext SyncContext;
        private bool isDisposed;

        public event EventHandler<ProcessStartedEventArgs> ProcessStarted;

        public SystemProcessWatcher(IProcessWatcher server, SynchronizationContext syncctx)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server), "Contract assertion not met: server != null");
            }

            this.Server = server;
            this.SyncContext = syncctx ?? new SynchronizationContext();
            this.Client = new CallbackThunk(this);

            this.SessionID = Process.GetCurrentProcess().SessionId;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;

            try
            {
                Server.Disconnect(Client);
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed to cleanly disconnect from server\r\n{ex}");
            }
            // free our root on Server so that it will be GC'd
            Server = null;
            // prevent us from being a root to whatever is listening on ProcessStarted
            Client.Disconnect();
        }

        public void Watch(string processName)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(SystemProcessWatcher));
            }

            Server.StartWatchingForProcess(SessionID, processName, Client);
        }

        private class CallbackThunk : IProcessWatcherClient
        {
            private SystemProcessWatcher Parent;

            public CallbackThunk(SystemProcessWatcher parent)
            {
                if (parent == null)
                {
                    throw new ArgumentNullException(nameof(parent), "Contract assertion not met: parent != null");
                }

                this.Parent = parent;
            }

            public void Disconnect()
            {
                this.Parent = null;
            }

            void IProcessWatcherClient.NotifyProcessCreated(int processId)
            {
                this.Parent?.NotifyProcessCreated(processId);
            }
        }

        private void NotifyProcessCreated(int processId)
        {
            SyncContext.Post((_1) =>
            {
                try
                {
                    var args = new ProcessStartedEventArgs(processId);
                    ProcessStarted?.Invoke(this, args);
                }
                catch (ArgumentException)
                {
                    // process not running, no op.
                    Log.Debug($"Received notification of a process start that is no longer running.  Process ID {processId}");
                }
            }, null);
        }

        public static SystemProcessWatcher GetCurrent(SynchronizationContext syncctx = null)
        {
            var watcher = ComInterop.CreateLocalServer<IProcessWatcher>(IPC.IpcConstants.ProcessWatcherProgID);
            return new SystemProcessWatcher(watcher, syncctx);
        }
    }
}
