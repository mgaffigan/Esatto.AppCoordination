using Esatto.AppCoordination.IPC;
using Esatto.Win32.Com;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace Esatto.AppCoordination.Coordinator;

internal sealed class StaticEntriesPublisher : IHostedService, IConnectionCallback
{
    private readonly IConnection App;
    private readonly StaticEntriesConfiguration Config;
    private readonly ILogger<StaticEntriesPublisher> Logger;

    public StaticEntriesPublisher(Coordinator coordinator, StaticEntriesConfiguration config,
        ILogger<StaticEntriesPublisher> logger)
    {
        this.App = ((ICoordinator)coordinator).Connect(this);
        this.Config = config;
        this.Logger = logger;

        Publish(config, logger);
    }

    private void Publish(StaticEntriesConfiguration config, ILogger<StaticEntriesPublisher> logger)
    {
        var entrySet = new EntrySet();
        foreach (var entry in config.Entries)
        {
            try
            {
                var addr = new CAddress(CPath.From(entry.Key), entry.Value.Key);
                entrySet.Entries.Add(addr.ToString(), JObject.FromObject(entry.Value.Properties));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Static registration {Key} is invalid, ignoring", entry.Key);
            }
        }
        App.Publish(entrySet.ToJson());
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        App.Dispose();
        return Task.CompletedTask;
    }

    void IConnectionCallback.Inform(string data)
    {
        // nop
    }

    string IConnectionCallback.Invoke(string path, string key, string payload, out bool failed)
    {
        var (registrationKey, _) = CPath.PopFirst(path);
        var config = Config.Entries[registrationKey];

        if (config.FactoryClsid is null)
        {
            throw new InvokeFaultException("Invoke is not supported by this entry");
        }

        var inst = ComInterop.CreateLocalServer<IStaticEntryHandler>(config.FactoryClsid.Value);

        try
        {
            ComInterop.CoAllowSetForegroundWindow(inst);
        }
        catch
        {
            Logger.LogInformation("CoAllowSetForegroundWindow failed");
        }

        return inst.Invoke(path, key, payload, out failed);
    }
}

public class StaticEntriesConfiguration
{
    public Dictionary<string, StaticEntryConfiguration> Entries { get; } = new();

    public static StaticEntriesConfiguration FromRegistry()
    {
        var config = new StaticEntriesConfiguration();

        // Add 64-bit apps
        const string path = @"SOFTWARE\In Touch Technologies\Esatto\AppCoordination\StaticEntries";
        using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var prog64 = hklm64.OpenSubKey(path, writable: false))
        {
            LoadFromKey(prog64, config);
        }

        // Add 32-bit apps
        using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (var prog32 = hklm32.OpenSubKey(path, writable: false))
        {
            LoadFromKey(prog32, config);
        }

        return config;
    }

    private static void LoadFromKey(RegistryKey? key, StaticEntriesConfiguration config)
    {
        if (key is null) return;

        foreach (var subkeyName in key.GetSubKeyNames())
        {
            using var subkey = key.OpenSubKey(subkeyName, writable: false)
                ?? throw new InvalidOperationException("TOCTOU in GetSubKeyNames?");

            config.Entries.Add(subkeyName, StaticEntryConfiguration.LoadFromKey(subkey, subkeyName));
        }
    }
}

public class StaticEntryConfiguration
{
    public string Key { get; set; } = "/";
    public Dictionary<string, JToken> Properties { get; } = new();
    public Guid? FactoryClsid { get; set; }

    public static StaticEntryConfiguration LoadFromKey(RegistryKey subkey, string subkeyName)
    {
        var entry = new StaticEntryConfiguration();
        foreach (var valueName in subkey.GetValueNames())
        {
            var value = subkey.GetValue(valueName);
            if (valueName == string.Empty)
            {
                var sValue = (string)value;
                CPath.Validate(sValue);
                entry.Key = sValue;
            }
            else if (string.Equals("CLSID", valueName, StringComparison.OrdinalIgnoreCase))
            {
                entry.FactoryClsid = Guid.Parse((string)value);
            }
            else
            {
                entry.Properties.Add(valueName, JToken.FromObject(value));
            }
        }
        entry.Key ??= CPath.From(subkeyName);
        return entry;
    }
}