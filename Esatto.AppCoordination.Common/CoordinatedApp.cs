using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace Esatto.AppCoordination;

public class CoordinatedApp : IDisposable
{
    private IConnection Connection;
    private readonly SynchronizationContext SyncCtx;
    private readonly ILogger Logger;
    private readonly NonEntryInvokableCollection NonEntryDelegates = new();
    private readonly bool SilentlyFail;
    private bool IsDisposed;

#if DEBUG
    private string ConstructionStackTrace = Environment.StackTrace;
#endif

    private PublishedEntryCollection PublishedEntries { get; }
    public ForeignEntryCollection ForeignEntities { get; }

    public CoordinatedApp(SynchronizationContext? syncCtx, bool silentlyFail, ILogger? logger)
    {
        logger ??= NullLogger.Instance;
        this.SyncCtx = syncCtx ?? new SynchronizationContext();
        this.Logger = logger;
        this.SilentlyFail = silentlyFail;

        this.ForeignEntities = new ForeignEntryCollection(this);

        // Connect is reentrant through CoordinatedAppThunk, so we have to be fully constructed by this point
        this.Connection = Connect(silentlyFail, logger);
        this.PublishedEntries = new PublishedEntryCollection(Connection, logger);

        AppDomain.CurrentDomain.ProcessExit += AppDomain_ProcessExit;
    }

    ~CoordinatedApp()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void AppDomain_ProcessExit(object? sender, EventArgs e)
    {
        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;

#if DEBUG
        if (!disposing)
        {
            Logger.LogInformation("Leaked CoordinatedApp instance.  Did you forget to call Dispose()? Stack at creation: {Stack}", ConstructionStackTrace);
        }
#endif

        try
        {
            try
            {
                Connection.Dispose();
            }
            catch when (disposing)
            {
                // nop
            }
            catch (Exception ex) when (SilentlyFail)
            {
                Logger.LogWarning(ex, "Failed to disconnect from coordinator.  Ignoring.");
            }

            AppDomain.CurrentDomain.ProcessExit -= AppDomain_ProcessExit;
        }
        catch
        {
            // nop
        }
    }

    private IConnection Connect(bool silentlyFail, ILogger logger)
    {
        IConnection ConnectInternal()
        {
            var coordinator = ComInterop.CreateLocalServer<ICoordinator>(new Guid(CoordinationConstants.CoordinatorClsid));
            return new DisconnectibleConnection(coordinator.Connect(new CoordinatedAppThunk(this)));
        }

        try
        {
            return ConnectInternal();
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x8001010D) /* RPC_E_CANTCALLOUT_ININPUTSYNCCALL */)
        {
            // Marshal to thread-pool to avoid RPC_E_CANTCALLOUT_ININPUTSYNCCALL.
            return Task.Run(ConnectInternal).Result;
        }
        catch (Exception ex) when (silentlyFail)
        {
            logger.LogWarning(ex, "Failed to connect to coordinator.  Ignoring.");
            return new NullConnection();
        }
    }

    public PublishedEntry Publish(string key, EntryValue value)
        => PublishedEntries.Publish(key, value, null);

    public PublishedEntry Publish(string key, EntryValue value, Func<string, string> action)
        => PublishedEntries.Publish(key, value, k => Task.FromResult(action(k)));

    public PublishedEntry Publish(string key, EntryValue value, Func<string, Task<string>> action)
        => PublishedEntries.Publish(key, value, action);

    public SingleInstanceApp GetSingleInstanceApp(string key, Guid? clsid = null)
        => new SingleInstanceApp(this, Logger, key, clsid, SyncCtx);

    internal void InvokeOneWay(CAddress address, string payload)
        => InvokeOneWay(null, address, payload);

    private void InvokeOneWay(string? replyPath, CAddress address, string payload)
    {
        var con = Connection;
        con.CoAllowSetForegroundWindowNoThrow();
        con.Invoke(replyPath, address.Path, address.Key, payload);
    }

    internal async Task<string> InvokeAsync(CAddress address, string payload, CancellationToken ct)
    {
        var respondBasePath = CPath.From("response", Guid.NewGuid().ToString("n"));
        var tcs = new TaskCompletionSource<string>();
        using var _1 = ct.Register(() => tcs.TrySetCanceled());
        using var _2 = NonEntryDelegates.Add(respondBasePath, (_, path, _, payload) =>
        {
            var (prefix, last) = CPath.PopLast(path);
            if (prefix != respondBasePath) throw new InvalidOperationException("Unexpected response path");

            if (last == ResponseStatusCodes.Success) tcs.TrySetResult(payload);
            else if (last == ResponseStatusCodes.Failed) tcs.TrySetException(InvokeFaultException.FromJson(payload));
            else if (last == ResponseStatusCodes.Cancelled) tcs.TrySetCanceled();
            else tcs.SetException(new InvalidOperationException($"Unexpected response status code {last}"));
        });

        await Task.Run(() => InvokeOneWay(respondBasePath, address, payload), ct);

        return await tcs.Task.ConfigureAwait(false);
    }

    private class CoordinatedAppThunk : IConnectionCallback
    {
        private CoordinatedApp Parent;
        private bool isInitialized;

        public CoordinatedAppThunk(CoordinatedApp parent)
        {
            this.Parent = parent;
        }

        // Called from NTA on COM Call stack
        public void Inform(string data)
        {
            var es = EntrySet.FromJson(data);
            if (!isInitialized)
            {
                // The first update must happen synchronously since the syncCtx might not be pumping yet
                isInitialized = true;
                Parent.ForeignEntities.Update(es);
            }
            else
            {
                Parent.SyncCtx.Post(_ =>
                {
                    try
                    {
                        Parent.ForeignEntities.Update(es);
                    }
                    catch (Exception ex)
                    {
                        Parent.Logger.LogWarning(ex, "Exception updating ForeignEntryCollection");
                    }
                }, null);
            }
        }

        // Called from NTA on COM Call stack
        public void HandleInvoke(string? sourcePath, string path, string key, string payload)
        {
            if (Parent.NonEntryDelegates.TryGet(path, out var nonEntry))
            {
                // Non-entry actions are invoked synchronously
                nonEntry(sourcePath, path, key, payload);
                return;
            }

            var address = new CAddress(path, key);
            Parent.SyncCtx.Post(_ => HandleInvokeInternal(sourcePath, address, payload), null);
        }

        // Called from SyncCtx, cannot throw
        private async void HandleInvokeInternal(string? sourcePath, CAddress address, string payload)
        {
            try
            {
                try
                {
                    var result = await Parent.PublishedEntries.HandleInvoke(address, payload).ConfigureAwait(false);
                    Respond(ResponseStatusCodes.Success, result);
                }
                catch (OperationCanceledException) { Respond(ResponseStatusCodes.Cancelled, ""); }
                catch (Exception ex)
                {
                    if (ex is not InvokeFaultException)
                    {
                        Parent.Logger.LogError(ex, "Error invoking action for entry {Address}", address);
                    }
                    Respond(ResponseStatusCodes.Failed, InvokeFaultException.ToJson(ex));
                }
            }
            catch (Exception ex)
            {
                Parent.Logger.LogError(ex, "Double fault invoking action for entry {0}", address);
            }

            void Respond(string statusCode, string payload)
            {
                if (sourcePath is null)
                {
                    Parent.Logger.LogInformation("No response path for action {Address} status {Status}: {Payload}", address, statusCode, payload);
                    return;
                }
                var responsePath = CPath.Suffix(sourcePath, statusCode);
                Parent.InvokeOneWay(null, new CAddress(responsePath, responsePath), payload);
            }
        }
    }
}
