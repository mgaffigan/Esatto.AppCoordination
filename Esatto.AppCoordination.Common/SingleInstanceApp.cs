using Esatto.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Esatto.AppCoordination;

public class SingleInstanceApp
{
    private readonly CoordinatedApp App;
    private readonly string Key;
    private readonly ILogger Logger;
    private readonly Guid? Clsid;
    private readonly SynchronizationContext SyncCtx;

    public bool RegisterSuspended { get; set; }

    public SingleInstanceApp(CoordinatedApp app, ILogger logger, string key, Guid? clsid, SynchronizationContext syncCtx)
    {
        this.App = app;
        this.Key = key;
        this.Logger = logger;
        this.Clsid = clsid;
        this.SyncCtx = syncCtx;
    }

    public static bool IsEmbedding(string[] args) => args.Any(a => string.Equals(a, "-embedding", StringComparison.OrdinalIgnoreCase));

    public bool TryInvokeActive(string[] args)
        => TryInvokeActive(JsonConvert.SerializeObject(args), out _);
    public bool TryInvokeActive(string payload,
#if NET
        [MaybeNullWhen(false)]
#endif
        out string response)
    {
        try
        {
            if (!TryGetAlive(out var entry))
            {
                response = null!;
                return false;
            }

            response = entry.Invoke(payload);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to invoke existing instance");

            response = InvokeFaultException.ToJson(ex);
            return false;
        }
    }

    public string Invoke(string payload)
    {
        if (!App.ForeignEntities.TryGetFirst(Key, out var entry))
        {
            throw new InvalidOperationException($"Key {Key} not found");
        }

        return entry.Invoke(payload);
    }

    public void Invoke(string[] args) => Invoke(JsonConvert.SerializeObject(args));

    public bool TryGetAlive(
#if NET
        [MaybeNullWhen(false)]
#endif
        out ForeignEntry entry)
    {
        return App.ForeignEntities.TryGetFirst(Key, out entry)
            && entry.Value.ContainsKey("Alive");
    }

    public PublishedEntry PublishEntry(EntryValue value, Action<string[]> action)
    {
        return PublishEntry(value, payload =>
        {
            action(JsonConvert.DeserializeObject<string[]>(payload)
                ?? throw new FormatException());
            return "";
        });
    }

    public PublishedEntry PublishEntry(EntryValue value)
    {
        MolestPublishedEntry(value);
        return App.Publish(Key, value);
    }

    public PublishedEntry PublishEntry(EntryValue value, Func<string, string> action)
    {
        MolestPublishedEntry(value);
        return App.Publish(Key, value, action);
    }

    public PublishedEntry PublishEntry(EntryValue value, Func<string, Task<string>> action)
    {
        MolestPublishedEntry(value);
        return App.Publish(Key, value, action);
    }

    private static void MolestPublishedEntry(EntryValue value)
    {
        value["Alive"] = true;
        if (!value.ContainsKey("Priority"))
        {
            value["Priority"] = 1_000;
        }
    }

    public StaticEntryHandler RegisterStatic(Action<string[]> action)
    {
        return RegisterStatic((_, _, payload) =>
        {
            action(JsonConvert.DeserializeObject<string[]>(payload)
                ?? throw new FormatException());
            return "";
        });
    }

    public StaticEntryHandler RegisterStatic(StaticEntryAction action)
    {
        return new StaticEntryHandler(Clsid ?? throw new InvalidOperationException("No static CLSID provided"),
            Logger, RegisterSuspended, action, SyncCtx);
    }

    public IDisposable PublishAndRegisterStatic(EntryValue value, Action<string[]> action)
    {
        var entry = PublishEntry(value, action);
        try
        {
            return new AggregateDisposable(entry, RegisterStatic(action));
        }
        catch
        {
            entry.Dispose();
            throw;
        }
    }

    private sealed class AggregateDisposable : IDisposable
    {
        private readonly IDisposable[] Disposables;

        public AggregateDisposable(params IDisposable[] disposables)
        {
            this.Disposables = disposables;
        }

        public void Dispose() => Disposables.DisposeAll();
    }
}
