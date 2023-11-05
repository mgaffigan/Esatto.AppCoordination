using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Esatto.AppCoordination;

public class CoordinatedApp : IDisposable
{
    private IConnection Connection;
    private readonly SynchronizationContext SyncCtx;
    private readonly ILogger Logger;
    private readonly bool SilentlyFail;

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
    }

    public void Dispose()
    {
        try
        {
            this.Connection.Dispose();
            this.Connection = new NullConnection();
        }
        catch (Exception ex) when (SilentlyFail)
        {
            Logger.LogWarning(ex, "Failed to disconnect from coordinator.  Ignoring.");
        }
    }

    private IConnection Connect(bool silentlyFail, ILogger logger)
    {
        try
        {
            var coordinator = ComInterop.CreateLocalServer<ICoordinator>(new Guid(CoordinationConstants.CoordinatorClsid));
            return coordinator.Connect(new CoordinatedAppThunk(this));
        }
        catch (Exception ex) when (silentlyFail)
        {
            logger.LogWarning(ex, "Failed to connect to coordinator.  Ignoring.");
            return new NullConnection();
        }
    }

    public PublishedEntry Publish(string key, EntryValue value, Func<string, string>? action = null)
    {
        return PublishedEntries.Publish(key, value, action);
    }

    public SingleInstanceApp GetSingleInstanceApp(string key, Guid? clsid = null)
    {
        return new SingleInstanceApp(this, Logger, key, clsid, SyncCtx);
    }

    internal string Invoke(CAddress address, string payload)
    {
        try
        {
            ComInterop.CoAllowSetForegroundWindow(Connection);
        }
        catch
        {
            Logger.LogInformation("CoAllowSetForegroundWindow failed");
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

        public string Invoke(string path, string key, string payload, out bool failed)
        {
            string? result = null;
            bool failed3 = false;
            Parent.SyncCtx.Send(_ =>
            {
                result = Parent.PublishedEntries.Invoke(new(path, key), payload, out var failed2);
                failed3 = failed2;
            }, null);
            failed = failed3;
            return result ?? throw new InvalidOperationException("Invoke did not return a value");
        }
    }
}