using Esatto.AppCoordination.IPC;
using Esatto.Utilities;
using Esatto.Win32.Com;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace Esatto.AppCoordination.Coordinator;

internal class ClientConnection : IDisposable
{
    private readonly AtomicReference<Dictionary<string, JToken>> Entries = new(new());

    private readonly ILogger Logger;
    private Coordinator? _Parent;
    private Coordinator Parent => _Parent ?? throw new ObjectDisposedException(nameof(ClientConnection));
    private IConnectionCallback? _Callback;
    private IConnectionCallback Callback => _Callback ?? throw new ObjectDisposedException(nameof(ClientConnection));

    public string ID { get; } = Guid.NewGuid().ToString("n");

    private ClientConnection(Coordinator parent, IConnectionCallback callback, ILogger logger)
    {
        this._Parent = parent;
        this._Callback = callback;
        this.Logger = logger;
    }

    public static (ClientConnection connection, IConnection thunk) Create(Coordinator parent, IConnectionCallback callback, ILogger logger, int information)
    {
        var con = new ClientConnection(parent, callback, logger);
        con.EntriesChangedInternal(information);

        // !!! nothing but the CCW may keep a reference to ConnectionThunk !!!
        return (con, new ConnectionThunk(con));
    }

    public void Dispose()
    {
        var parent = _Parent;
        _Parent = null;
        _Callback = null;
        parent?.Disconnect(this);
        Entries.Value = new();
    }

    public string Invoke(string path, string key, string payload, out bool failed)
    {
        try
        {
            ComInterop.CoAllowSetForegroundWindow(Callback);
        }
        catch
        {
            Logger.LogInformation("CoAllowSetForegroundWindow failed");
        }

        try
        {
            return Callback.Invoke(path, key, payload, out failed);
        }
        catch (COMException ex) when (ex.IsServerDisconnected())
        {
            Logger.LogError(ex, "Disconnecting {ID} due to exception", ID);
            Dispose();
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Exception in Invoke to {Path}", path);
            throw;
        }
    }

    // noexcept, called on arbitrary connection stack
    public void EntriesChanged(int information)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                EntriesChangedInternal(information);
            }
            catch (InformationOutOfDateException)
            {
                // A new update came in, die. Another ThreadPool work item with
                // up-to-date information will take care of it
            }
            catch (COMException ex) when (ex.IsServerDisconnected())
            {
                Logger.LogError(ex, "Disconnecting {ID} due to exception", ID);
                Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error publishing entries to {ID}", ID);
            }
        }, null);
    }

    private void EntriesChangedInternal(int information)
    {
        Callback.Inform(Parent.GetViewFor(this, information).ToJson());
    }

    private void Publish(EntrySet data)
    {
        Entries.Value = data.Entries.ToDictionary(
            kvp => 
            {
                var addr = new CAddress(kvp.Key);
                return new CAddress(CPath.Prefix(ID, addr.Path), addr.Key).ToString();
            },
            kvp => kvp.Value);
        Parent.OnEntriesChanged(this);
    }

    internal void GetView(EntrySet results)
    {
        results.Entries.AddRange(Entries.Value);
    }

    // Thunk will be GC'd once connection is released
    // !!! nothing but the CCW may keep a reference to ConnectionThunk !!!
    private sealed class ConnectionThunk : IConnection
    {
        private ClientConnection Connection;

        public ConnectionThunk(ClientConnection con)
        {
            this.Connection = con;
        }

        ~ConnectionThunk()
        {
            Dispose();
        }

        public void Dispose() => Connection.Dispose();

        public void Publish(string data)
        {
            // we want to throw on format exceptions
            Connection.Publish(EntrySet.FromJson(data));
        }

        public string Invoke(string path, string key, string payload, out bool failed)
        {
            return Connection.Parent.Invoke(path, key, payload, out failed);
        }
    }
}
