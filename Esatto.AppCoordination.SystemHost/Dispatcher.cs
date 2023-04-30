using System;
using System.Collections.Generic;
using System.Linq;

namespace Esatto.AppCoordination.SystemHost
{
    internal sealed class Dispatcher
    {
        private readonly object syncClients = new object();
        private readonly List<WeakReference<ProcessWatcher>> Clients = new List<WeakReference<ProcessWatcher>>();

        public bool CheckAlive()
        {
            lock (syncClients)
            {
                Clients.RemoveAll(c =>
                {
                    ProcessWatcher _1;
                    // if we can't get the target then it is already collected
                    return !c.TryGetTarget(out _1);
                });

                return Clients.Any();
            }
        }

        public void AddClient(ProcessWatcher processWatcher)
        {
            if (processWatcher == null)
            {
                throw new ArgumentNullException(nameof(processWatcher), "Contract assertion not met: processWatcher != null");
            }

            lock (syncClients)
            {
                Clients.Add(new WeakReference<ProcessWatcher>(processWatcher));
            }
        }

        public void Evaluate(int processId, string processName, int sessionId)
        {
            List<ProcessWatcher> aliveClients = new List<ProcessWatcher>();
            lock (syncClients)
            {
                foreach (var client in this.Clients)
                {
                    ProcessWatcher watcher;
                    if (client.TryGetTarget(out watcher))
                    {
                        aliveClients.Add(watcher);
                    }
                }
            }

            // we call the client outside the lock since they may call back to "Disconnect" on another thread
            foreach (var client in aliveClients)
            {
                client.Evaluate(processId, processName, sessionId);
            }
        }
    }
}