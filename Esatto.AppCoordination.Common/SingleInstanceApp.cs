using Microsoft.Extensions.Logging;
using System.Text.Json;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

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
        => TryInvokeActive(JsonSerializer.Serialize(args, CoordinationConstants.JsonSerializerOptions));
    public bool TryInvokeActive(string payload)
    {
        try
        {
            if (!TryGetAlive(out var entry))
            {
                return false;
            }

            entry.InvokeOneWay(payload);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to invoke existing instance");
            return false;
        }
    }

    public Task<string> InvokeAsync(string payload, CancellationToken ct = default)
    {
        if (!App.ForeignEntities.TryGetFirst(Key, out var entry))
        {
            throw new InvalidOperationException($"Key {Key} not found");
        }

        return entry.InvokeAsync(payload, ct);
    }

    public Task<string> InvokeAsync(string[] args)
        => InvokeAsync(JsonSerializer.Serialize(args, CoordinationConstants.JsonSerializerOptions));

    public bool TryGetAlive(
#if NET
        [MaybeNullWhen(false)]
#endif
        out ForeignEntry entry)
    {
        return App.ForeignEntities.TryGetFirst(Key, out entry)
            && entry.Value.ContainsKey("Alive");
    }

    public void InvokeOneWay(string payload)
    {
        if (TryInvokeActive(payload)) return;

        if (!App.ForeignEntities.TryGetFirst(Key, out var entry))
        {
            throw new InvalidOperationException($"Key {Key} not found");
        }

        entry.InvokeOneWay(payload);
    }

    public void InvokeOneWay(string[] args)
        => InvokeOneWay(JsonSerializer.Serialize(args, CoordinationConstants.JsonSerializerOptions));

    public PublishedEntry PublishEntry(EntryValue value, Func<string[], Task<string>> action)
    {
        return PublishEntry(value, payload =>
        {
            return action(JsonSerializer.Deserialize<string[]>(payload, CoordinationConstants.JsonSerializerOptions)
                ?? throw new FormatException());
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

    public StaticEntryHandler RegisterStatic(Func<string[], Task<string>> action)
    {
        return RegisterStatic((_, _, payload) =>
        {
            return action(JsonSerializer.Deserialize<string[]>(payload, CoordinationConstants.JsonSerializerOptions)
                ?? throw new FormatException());
        });
    }

    public StaticEntryHandler RegisterStatic(StaticEntryAction action)
    {
        return new StaticEntryHandler(Clsid ?? throw new InvalidOperationException("No static CLSID provided"),
            Logger, RegisterSuspended, action, SyncCtx, App);
    }

    public IDisposable PublishAndRegisterStatic(EntryValue value, Func<string[], Task<string>> action)
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

    public IDisposable PublishAndRegisterStatic(EntryValue value, Action<string[]> action)
    {
        var entry = PublishEntry(value, Thunk);
        try
        {
            return new AggregateDisposable(entry, RegisterStatic(Thunk));
        }
        catch
        {
            entry.Dispose();
            throw;
        }

        Task<string> Thunk(string[] args)
        {
            action(args);
            return Task.FromResult("");
        }
    }
}
