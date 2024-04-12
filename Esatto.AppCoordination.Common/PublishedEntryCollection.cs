using Esatto.AppCoordination.IPC;
using Microsoft.Extensions.Logging;

namespace Esatto.AppCoordination;

internal class PublishedEntryCollection
{
    private readonly object SyncPublished = new();
    // WeakReference so that the finalizer will run to clean things up if someone
    // forgets to dispose their PublishedEntry
    private Dictionary<CAddress, WeakReference<PublishedEntry>> PublishedEntries = new();
    private int Information;
    private int NextEntityID;

    private readonly IConnection Connection;
    private readonly ILogger Logger;

    public PublishedEntryCollection(IConnection connection, ILogger logger)
    {
        this.Connection = connection;
        this.Logger = logger;
    }

    public PublishedEntry Publish(string key, EntryValue value, Func<string, Task<string>>? action)
    {
        PublishedEntry entry;
        lock (SyncPublished)
        {
            var address = new CAddress(CPath.From(unchecked(NextEntityID++).ToString("x8")), key);
            entry = new PublishedEntry(this, address, value, action);
            PublishedEntries.Add(address, new WeakReference<PublishedEntry>(entry));
        }
        Update();
        return entry;
    }

    public Task<string> Invoke(CAddress address, string payload)
    {
        PublishedEntry? entry;
        lock (SyncPublished)
        {
            if (!PublishedEntries[address].TryGetTarget(out entry))
            {
                throw new KeyNotFoundException();
            }
        }

        var action = entry.Action ?? throw new NotSupportedException("No action defined for entry");
        return action(payload);
    }

    internal void RemoveEntry(CAddress address)
    {
        lock (SyncPublished)
        {
            PublishedEntries.Remove(address);
        }
        Update();
    }

    internal void UpdatePublished()
    {
        Update();
    }

    private void Update()
    {
        var information = Interlocked.Increment(ref Information);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var entries = new EntrySet();
            lock (SyncPublished)
            {
                var aliveEntries = new List<PublishedEntry>();
                foreach (var entry in PublishedEntries)
                {
                    if (entry.Value.TryGetTarget(out var pe))
                    {
                        entries.Entries.Add(pe.Address.ToString(), pe._Value.Value);
                    }
                }
            }

            // Check if we are racing another update
            Thread.MemoryBarrier();
            if (Information != information)
            {
                return;
            }

            // Publish
            try
            {
                Connection.Publish(entries.ToJson());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error publishing entries to coordinator");
            }
        }, null);
    }
}
