using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.SystemHost
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId(IPC.IpcConstants.ProcessWatcherProgID)]
    public sealed class ProcessWatcher : IProcessWatcher
    {
        private Dispatcher Dispatcher;

        private readonly object syncClients = new object();
        private readonly List<ProcessWatcherClientConnection> Clients = new List<ProcessWatcherClientConnection>();

        public ProcessWatcher()
        {
            // this constructor only exists to make it eligable for COM Registration
            throw new NotSupportedException();
        }

        internal ProcessWatcher(Dispatcher dispatcher)
        {
            this.Dispatcher = dispatcher;
            this.Dispatcher.AddClient(this);
        }

        internal class ProcessWatcherClientConnection
        {
            public readonly IProcessWatcherClient Client;
            public readonly string WatchedProcessName;
            public readonly int SessionId;

            public ProcessWatcherClientConnection(IProcessWatcherClient client, string processName, int sessionId)
            {
                if (client == null)
                {
                    throw new ArgumentNullException(nameof(client), "Contract assertion not met: client != null");
                }
                if (String.IsNullOrEmpty(processName))
                {
                    throw new ArgumentException("Contract assertion not met: !String.IsNullOrEmpty(processName)", nameof(processName));
                }

                this.Client = client;
                this.WatchedProcessName = processName;
                this.SessionId = sessionId;
            }
        }

        public void Evaluate(int processId, string processName, int sessionId)
        {
            // Process.ProcessName is without an extension, WMI is with an extension, so we search for either
            string procNameNoExtension = null;
            try
            {
                procNameNoExtension = Path.GetFileNameWithoutExtension(processName);
            }
            catch { /* noop on wierd process names */ }

            IEnumerable<ProcessWatcherClientConnection> results;
            lock (syncClients)
            {
                results = Clients.Where(p =>
                    (string.Equals(p.WatchedProcessName, processName, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(p.WatchedProcessName, procNameNoExtension, StringComparison.InvariantCultureIgnoreCase))
                    && p.SessionId == sessionId).ToArray();
            }

            // we call the client outside the lock since they may call back to "Disconnect" on another thread
            foreach (var client in results)
            {
                try
                {
                    client.Client.NotifyProcessCreated(processId);
                }
                catch
                {
                    Log.Info($"Failed to notify client watching for process {processName}", 1004);
                    lock (syncClients)
                    {
                        Clients.Remove(client);
                    }
                }
            }
        }

        void IProcessWatcher.StartWatchingForProcess(int sessionId, string processName, IProcessWatcherClient client)
        {
            lock (syncClients)
            {
                if (Clients.Any(c => c.Client == client && c.WatchedProcessName == processName))
                {
                    Log.Info($"Duplicate registration from client for process name {processName}", 1005);
                }

                Clients.Add(new ProcessWatcherClientConnection(client, processName, sessionId));
            }
        }

        void IProcessWatcher.Disconnect(IProcessWatcherClient client)
        {
            lock (syncClients)
            {
                Clients.RemoveAll(c => c.Client == client);
            }
        }

        #region Registration

        internal static string OurAppID => typeof(Program).GUID.ToString("B").ToUpper();
        internal static string OurExeName => Path.GetFileName(Assembly.GetExecutingAssembly().Location);

        internal static byte[] AccessPermBlob = new byte[]
        {
            0x01, 0x00, 0x04, 0x80, 0x70, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x5c, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x05, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x07, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x0a, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x14, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x00, 0x07,
            0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x20, 0x00, 0x00, 0x00, 0x20, 0x02, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x05, 0x20, 0x00, 0x00, 0x00, 0x20, 0x02, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x20, 0x00, 0x00, 0x00, 0x20, 0x02, 0x00,
            0x00
        };

        internal static byte[] LaunchPermBlob = new byte[]
        {
            0x01, 0x00, 0x04, 0x80, 0x70, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x5c, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x05, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x20, 0x00, 0x00, 0x00,
            0x20, 0x02, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x0b, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x14, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x04, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x05, 0x20, 0x00, 0x00, 0x00, 0x20, 0x02, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x20, 0x00, 0x00, 0x00, 0x20, 0x02, 0x00,
            0x00
        };

        [ComRegisterFunction]
        internal static void RegasmRegisterLocalServer(string path)
        {
            // path is HKEY_CLASSES_ROOT\\CLSID\\{clsid}", we only want CLSID...
            path = path.Substring("HKEY_CLASSES_ROOT\\".Length);
            using (RegistryKey keyCLSID = Registry.ClassesRoot.OpenSubKey(path, writable: true))
            {
                // Remove the auto-generated InprocServer32 key after registration
                // (REGASM puts it there but we are going out-of-proc).
                keyCLSID.DeleteSubKeyTree("InprocServer32");

                // add an appid
                keyCLSID.SetValue("AppID", OurAppID);
            }

            using (RegistryKey rkAppID = Registry.ClassesRoot.CreateSubKey($"AppID\\{OurAppID}"))
            {
                rkAppID.SetValue("LocalService", SystemHostService.SERVICE_NAME);
                rkAppID.SetValue("", "Esatto Application Coordination System Host");
                rkAppID.SetValue("AccessPermission", AccessPermBlob);
                rkAppID.SetValue("LaunchPermission", LaunchPermBlob);
            }

            using (RegistryKey rkAppExe = Registry.ClassesRoot.CreateSubKey($"AppID\\{OurExeName}"))
            {
                rkAppExe.SetValue("AppID", OurAppID);
            }
        }

        [ComUnregisterFunction]
        internal static void RegasmUnregisterLocalServer(string path)
        {
            // path is HKEY_CLASSES_ROOT\\CLSID\\{clsid}", we only want CLSID...
            path = path.Substring("HKEY_CLASSES_ROOT\\".Length);
            Registry.ClassesRoot.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            Registry.ClassesRoot.DeleteSubKeyTree($"AppID\\{OurAppID}", throwOnMissingSubKey: false);
            Registry.ClassesRoot.DeleteSubKeyTree($"AppID\\{OurExeName}", throwOnMissingSubKey: false);
        }

        #endregion
    }
}
