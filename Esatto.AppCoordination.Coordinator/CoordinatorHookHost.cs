using Esatto.AppCoordination.Extensibility;
using Esatto.AppCoordination.IPC;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    internal sealed class CoordinatorHookHost : IDisposable
    {
        private readonly ICoordinator Coordinator;
        private readonly List<ICoordinatorHook> Hooks;
        private const string CoordinatorHooks = @"SOFTWARE\In Touch Technologies\Esatto\App Coordination\CoordinatorHooks";

        public CoordinatorHookHost(ICoordinator coordinator)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator), "Contract assertion not met: coordinator != null");
            }

            this.Coordinator = coordinator;
            this.Hooks = new List<ICoordinatorHook>();

            using (var rkHooks = Registry.LocalMachine.OpenSubKey(CoordinatorHooks, writable: false))
            {
                foreach (var keyName in (rkHooks?.GetSubKeyNames() ?? new string[0]))
                {
                    string typeName = null;
                    string codebase = null;
                    try
                    {
                        using (var rkHook = rkHooks.OpenSubKey(keyName))
                        {
                            typeName = (string)rkHook.GetValue("TypeName");
                            codebase = (string)rkHook.GetValue("Codebase");
                        }

                        if (codebase != null)
                        {
                            Assembly.LoadFrom(codebase);
                        }

                        var type = Type.GetType(typeName,
                            assemblyResolver: (name) =>
                            {
                                // Returns the assembly of the type by enumerating loaded assemblies
                                // in the app domain
                                return AppDomain.CurrentDomain.GetAssemblies().Where(z => z.FullName == name.FullName).FirstOrDefault();
                            }, typeResolver: null, throwOnError: true);
                        var inst = (ICoordinatorHook)Activator.CreateInstance(type);
                        inst.Initialize(coordinator);
                        this.Hooks.Add(inst);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op, unneeded hook
                    }
                    catch (TargetInvocationException iex) when (iex.InnerException is OperationCanceledException)
                    {
                        // no-op, unneeded hook
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Exception while initializing Coordinator Hook '{typeName}' from '{codebase ?? "GAC"}' ({keyName})\r\n\r\n{ex}", 1517);
                    }
                }
            }
        }

        public void NotifyNewDeployment(IDeploymentCoordinator coordinator)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator), "Contract assertion not met: coordinator != null");
            }

            foreach (var hook in this.Hooks)
            {
                hook.NotifyNewDeployment(coordinator);
            }
        }

        public void Dispose()
        {
            foreach (var hook in this.Hooks)
            {
                try
                {
                    hook.Dispose();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Exception while disposing coordinator hook '{hook.GetType().AssemblyQualifiedName}'", ex);
                }
            }
        }
    }
}
