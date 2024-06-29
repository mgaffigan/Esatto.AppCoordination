using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Esatto.AppCoordination;

public class CoordinatedApp : IDisposable
{
    private IConnection Connection;
    private readonly SynchronizationContext SyncCtx;
    private readonly ILogger Logger;
    private readonly bool SilentlyFail;
    private bool IsDisposed;

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

    internal string Invoke(CAddress address, string payload)
    {
        try
        {
            if (Connection is DisconnectibleConnection dc && dc.Inner is IConnection inner)
            {
                ComInterop.CoAllowSetForegroundWindow(inner);
            }
            else
            {
                ComInterop.CoAllowSetForegroundWindow(Connection);
            }
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "CoAllowSetForegroundWindow failed");
        }

        var result = Connection.Invoke(address.Path, address.Key, payload, out var failed);
        if (failed)
        {
            throw InvokeFaultException.FromJson(result);
        }
        return result;
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
        public string Invoke(string path, string key, string payload, out bool failed)
        {
            var address = new CAddress(path, key);
            Task<string>? t = null;
            Parent.SyncCtx.Send(_ =>
            {
                t = Parent.PublishedEntries.Invoke(address, payload);
            }, null);

            if (t is null)
            {
                throw new InvalidOperationException("Invoke did not return a value");
            }

            try
            {
                var result = t.GetAwaiter().GetResult();
                failed = false;
                return result;
            }
            catch (Exception ex)
            {
                if (ex is not InvokeFaultException)
                {
                    Parent.Logger.LogError(ex, "Error invoking action for entry {0}", address);
                }

                failed = true;
                return InvokeFaultException.ToJson(ex);
            }
        }
    }
}