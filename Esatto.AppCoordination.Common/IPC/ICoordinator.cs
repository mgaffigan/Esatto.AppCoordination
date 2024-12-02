using Esatto.Win32.Com;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Esatto.AppCoordination.IPC;

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("06E26DC0-FA8C-496B-85CA-FC7E0AD4B4E5")]
public interface ICoordinator
{
    IConnection Connect(IConnectionCallback callback);
}

public class NullCoordinator : ICoordinator
{
    public IConnection Connect(IConnectionCallback callback)
    {
        callback.Inform(new EntrySet().ToJson());
        return new NullConnection();
    }
}

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("D29FBC53-6632-4826-8917-D1A95AE6471E")]
public interface IConnection : IDisposable
{
    /// <summary>
    /// Replace the set of entries published by this connection
    /// </summary>
    /// <param name="data">the new entries</param>
    void Publish(string data);
    void Invoke(string? sourcePath, string path, string key, string payload);
}

public static class ResponseStatusCodes
{
    public const string
        Success = "SUCCESS",
        Failed = "FAILED",
        Cancelled = "CANCELLED";
}

public class DisconnectibleConnection : IConnection
{
    public IConnection? Inner { get; private set; }

    public DisconnectibleConnection(IConnection? inner)
    {
        this.Inner = inner;
    }

    public void Dispose()
    {
        var t = this.Inner;
        this.Inner = null;
        t?.Dispose();
    }

    public void Invoke(string? sourcePath, string path, string key, string payload)
    {
        var inner = this.Inner ?? throw new ObjectDisposedException(nameof(DisconnectibleConnection));
        inner.Invoke(sourcePath, path, key, payload);
    }

    public void Publish(string data)
    {
        Inner?.Publish(data);
    }
}

public class NullConnection : DisconnectibleConnection
{
    public NullConnection() 
        : base(null)
    {
        // nop
    }
}

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("30D76095-78D6-4855-AC96-30FC18E9B0F8")]
public interface IConnectionCallback
{
    /// <summary>
    /// Update a client about the current set of entries known to the coordinator
    /// </summary>
    /// <param name="data"></param>
    void Inform(string data);
    void HandleInvoke(string? sourcePath, string path, string key, string payload);
}

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("6AB39F0A-9D96-4271-8A9A-ADD071F6743E")]
public interface IStaticEntryHandler
{
    void HandleInvoke(string? sourcePath, string path, string key, string payload);
}

#nullable disable

public class EntrySet
{
    public Dictionary<CAddress, EntryValue> Entries { get; } = new();

    private class EntrySetDto
    {
        public Dictionary<string, JsonNode> Entries { get; set; } = new();
    }

    public string ToJson()
    {
        var dto = new EntrySetDto();
        foreach (var entry in Entries)
        {
            dto.Entries.Add(entry.Key.ToString(), entry.Value.Value);
        }
        return JsonSerializer.Serialize(dto, CoordinationConstants.JsonSerializerOptions);
    }

    public static EntrySet FromJson(string s)
    {
        var dto = JsonSerializer.Deserialize<EntrySetDto>(s, CoordinationConstants.JsonSerializerOptions)
            ?? throw new FormatException();
        if (dto.Entries is null) throw new FormatException();
        var result = new EntrySet();
        foreach (var source in dto.Entries)
        {
            result.Entries.Add(new CAddress(source.Key), new EntryValue(source.Value));
        }
        return result;
    }
}

#nullable restore

public static class IConnectionExtensions
{
    public static void CoAllowSetForegroundWindowNoThrow(this IConnection connection)
        => CoAllowSetForegroundWindowNoThrowInternal(connection);

    public static void CoAllowSetForegroundWindowNoThrow(this IConnectionCallback callback)
        => CoAllowSetForegroundWindowNoThrowInternal(callback);

    public static void CoAllowSetForegroundWindowNoThrow(this IStaticEntryHandler coordinator)
        => CoAllowSetForegroundWindowNoThrowInternal(coordinator);

    private static void CoAllowSetForegroundWindowNoThrowInternal(object? obj)
    {
        try
        {
            if (obj is DisconnectibleConnection dc)
            {
                obj = dc.Inner;
            }
            
            if (obj is not null && Marshal.IsComObject(obj))
            {
                ComInterop.CoAllowSetForegroundWindow(obj);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Could not call CoAllowSetForegroundWindow: " + ex.Message);
        }
    }
}