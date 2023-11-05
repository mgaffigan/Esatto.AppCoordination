using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination
{
    public delegate string StaticEntryAction(string uuid, string key, string payload);

    public class StaticEntryHandler : IDisposable, IStaticEntryHandler
    {
        private readonly ILogger Logger;
        private readonly StaticEntryAction Action;
        private readonly SynchronizationContext SyncCtx;
        private readonly ClassObjectRegistration Reg;

        public StaticEntryHandler(Guid clsid, ILogger logger, bool registerSuspended, StaticEntryAction action, SynchronizationContext syncCtx)
        {
            this.Logger = logger;
            this.Action = action;
            this.SyncCtx = syncCtx;

            this.Reg = new ClassObjectRegistration(clsid, ComInterop.CreateStaClassFactoryFor(() => this),
                CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | (registerSuspended ? REGCLS.SUSPENDED : 0));
        }

        public void Dispose() => this.Reg.Dispose();

        string IStaticEntryHandler.Invoke(string path, string key, string payload, out bool failed)
        {
            try
            {
                var (uuid, _) = CPath.PopFirst(path);
                string result = null!;
                SyncCtx.Send(_ =>
                {
                    result = Action(uuid, key, payload);
                }, null);
                failed = false;
                return result;
            }
            catch (Exception ex)
            {
                if (ex is not InvokeFaultException)
                {
                    Logger.LogError(ex, "Error invoking static action for entry {Path}", path);
                }

                failed = true;
                return InvokeFaultException.ToJson(ex);
            }
        }
    }
}
