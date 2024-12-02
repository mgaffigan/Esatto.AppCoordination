using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;

namespace Esatto.AppCoordination
{
    public delegate Task<string> StaticEntryAction(string uuid, string key, string payload);

    public class StaticEntryHandler : IDisposable, IStaticEntryHandler
    {
        private readonly ILogger Logger;
        private readonly StaticEntryAction Action;
        private readonly SynchronizationContext SyncCtx;
        private readonly CoordinatedApp? App;
        private readonly ClassObjectRegistration Reg;

        public StaticEntryHandler(Guid clsid, ILogger logger, bool registerSuspended,
            StaticEntryAction action, SynchronizationContext syncCtx, CoordinatedApp? app)
        {
            this.Logger = logger;
            this.Action = action;
            this.SyncCtx = syncCtx;
            this.App = app;

            this.Reg = new ClassObjectRegistration(clsid, ComInterop.CreateStaClassFactoryFor(() => this),
                CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | (registerSuspended ? REGCLS.SUSPENDED : 0));
        }

        public void Dispose() => this.Reg.Dispose();

        void IStaticEntryHandler.HandleInvoke(string? sourcePath, string path, string key, string payload)
        {
            var (uuid, _) = CPath.PopFirst(path);
            var address = new CAddress(path, key);
            SyncCtx.Post(_ => HandleInvokeAsync(sourcePath, uuid, address, payload), null);
        }

        private async void HandleInvokeAsync(string? sourcePath, string uuid, CAddress address, string payload)
        {
            try
            {
                try
                {
                    var result = await Action(uuid, address.Key, payload);
                    Respond(ResponseStatusCodes.Success, result);
                }
                catch (OperationCanceledException) { Respond(ResponseStatusCodes.Cancelled, ""); }
                catch (Exception ex)
                {
                    if (ex is not InvokeFaultException)
                    {
                        Logger.LogError(ex, "Error invoking action for entry {Address}", address);
                    }
                    Respond(ResponseStatusCodes.Failed, InvokeFaultException.ToJson(ex));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Double fault invoking action for entry {0}", address);
            }

            void Respond(string statusCode, string payload)
            {
                if (sourcePath is null || App is null)
                {
                    Logger.LogInformation("No response path for action {Address} " +
                        "status {Status}: {Payload}", address, statusCode, payload);
                    return;
                }
                var responsePath = CPath.Suffix(sourcePath, statusCode);
                App.InvokeOneWay(new CAddress(responsePath, responsePath), payload);
            }
        }
    }
}
