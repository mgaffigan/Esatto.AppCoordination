using Esatto.AppCoordination;
using Esatto.AppCoordination.IPC;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    // StandardOleMarshalObject keeps us single-threaded on the UI thread
    // https://msdn.microsoft.com/en-us/library/74169f59(v=vs.110).aspx

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId(IpcConstants.CoordinatorProgID)]
    public sealed class Coordinator : StandardOleMarshalObject, ICoordinator
    {
        private readonly Dictionary<string, DeploymentCoordinator> DeploymentCoordinators;
        private readonly ThreadAssert MainThread;

        internal event EventHandler<NewDeploymentCoordinatorEventArgs> NewDeploymentCreated;

        private SessionRecord _AmbientSession;
        public SessionRecord AmbientSession
        {
            get
            {
                MainThread.Assert();

                return _AmbientSession;
            }
            set
            {
                MainThread.Assert();

                _AmbientSession = value;
            }
        }

        public Coordinator()
        {
            this.MainThread = new ThreadAssert();
            this.DeploymentCoordinators = new Dictionary<string, DeploymentCoordinator>(StringComparer.InvariantCultureIgnoreCase);
        }

        public IDeploymentCoordinator GetCoordinatorForDeployment(string deployment)
        {
            MainThread.Assert();

            DeploymentCoordinator result;
            if (!DeploymentCoordinators.TryGetValue(deployment, out result))
            {
                result = new DeploymentCoordinator(deployment);
                NewDeploymentCreated?.Invoke(this, new NewDeploymentCoordinatorEventArgs(result));
                DeploymentCoordinators.Add(deployment, result);
            }

            return result;
        }

        #region Registration

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

                // Create "LocalServer32" under the CLSID key
                using (RegistryKey subkey = keyCLSID.CreateSubKey("LocalServer32"))
                {
                    subkey.SetValue("", Assembly.GetExecutingAssembly().Location, RegistryValueKind.String);
                }
            }
        }

        [ComUnregisterFunction]
        internal static void RegasmUnregisterLocalServer(string path)
        {
            // path is HKEY_CLASSES_ROOT\\CLSID\\{clsid}", we only want CLSID...
            path = path.Substring("HKEY_CLASSES_ROOT\\".Length);
            Registry.ClassesRoot.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }

        #endregion
    }
}
