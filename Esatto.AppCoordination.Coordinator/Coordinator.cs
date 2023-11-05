using Esatto.AppCoordination.IPC;
using Microsoft.Extensions.Logging;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Runtime.ConstrainedExecution;
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

    internal string Invoke(string path, string key, string payload, out bool failed)
    {
        try
        {
            var (first, rest) = CPath.PopFirst(path);
            return Connections[first].Invoke(rest, key, payload, out failed);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dispatching invoke to {Path}:{Key} failed", path, key);
            failed = true;
            return InvokeFaultException.ToJson(ex);
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