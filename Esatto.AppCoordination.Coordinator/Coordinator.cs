using Esatto.AppCoordination.IPC;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Esatto.AppCoordination.Coordinator;

// free-threaded
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
[Guid(CoordinationConstants.CoordinatorClsid)]
internal class Coordinator : ICoordinator
{
    private readonly ConcurrentDictionary<string, ClientConnection> Connections = new();
    private readonly ILogger Logger;
    private int Information;

    public Coordinator(ILogger<Coordinator> logger)
    {
        this.Logger = logger;
    }

    IConnection ICoordinator.Connect(IConnectionCallback callback)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        var (con, thunk) = ClientConnection.Create(this, callback, Logger, Information);
        if (!Connections.TryAdd(con.ID, con))
        {
            throw new InvalidOperationException("Duplicate connection ID");
        }

        // Don't need to call EntriesChanged on other threads since no
        // entries are initially published
        return thunk;
    }

    internal void Disconnect(ClientConnection con)
    {
        if (Connections.TryRemove(con.ID, out _))
        {
            var now = Interlocked.Increment(ref Information);
            foreach (var other in Connections.Values)
            {
                other.EntriesChanged(now);
            }
        }
    }

    // noexcept, called on arbitrary connection stack
    internal void OnEntriesChanged(ClientConnection con)
    {
        var now = Interlocked.Increment(ref Information);
        foreach (var other in Connections.Values)
        {
            if (other == con) continue;
            other.EntriesChanged(Information);
        }
    }

    internal void Invoke(string? sourcePath, string path, string key, string payload)
    {
        try
        {
            var (first, rest) = CPath.PopFirst(path);
            if (!Connections.TryGetValue(first, out var con))
            {
                throw new InvokeFaultException("No such connection");
            }
            con.HandleInvoke(sourcePath, rest, key, payload);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dispatching invoke to {Path}:{Key} failed", path, key);

            // Report the failure to the caller, if existing
            if (sourcePath is not null)
            {
                sourcePath = CPath.Suffix(sourcePath, ResponseStatusCodes.Failed);
                // This cannot recurse since sourcePath is null on errors
                try { Invoke(null, sourcePath, sourcePath, InvokeFaultException.ToJson(ex)); }
                catch (Exception ex2)
                {
                    Logger.LogWarning(ex2, "Double fault sending InvokeFaultException to {Path}", sourcePath);
                }
            }
        }
    }

    // noexcept, called on threadpool
    internal EntrySet GetViewFor(ClientConnection con, int information)
    {
        var entries = new EntrySet();
        foreach (var otherCon in Connections.Values)
        {
            if (con == otherCon) continue;
            otherCon.GetView(entries);
        }
        Thread.MemoryBarrier();
        if (information != Information)
        {
            throw new InformationOutOfDateException();
        }
        return entries;
    }
}


[Serializable]
public class InformationOutOfDateException : Exception
{
    public InformationOutOfDateException() { }
    public InformationOutOfDateException(string message) : base(message) { }
    public InformationOutOfDateException(string message, Exception inner) : base(message, inner) { }
    protected InformationOutOfDateException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}