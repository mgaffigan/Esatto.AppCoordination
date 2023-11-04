using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Esatto.AppCoordination.IPC;

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("06E26DC0-FA8C-496B-85CA-FC7E0AD4B4E4")]
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
[Guid("D29FBC53-6632-4826-8917-D1A95AE6471D")]
public interface IConnection : IDisposable
{
    /// <summary>
    /// Replace the set of entries published by this connection
    /// </summary>
    /// <param name="data">the new entries</param>
    void Publish(string data);
    string Invoke(string path, string key, string payload, out bool failed);
}

public class NullConnection : IConnection
{
    public void Dispose()
    {
        // nop
    }

    public string Invoke(string path, string key, string payload, out bool failed)
    {
        failed = true;
        return "";
    }

    public void Publish(string data)
    {
        // nop
    }
}

[ComVisible(true), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("30D76095-78D6-4855-AC96-30FC18E9B0F7")]
public interface IConnectionCallback
{
    /// <summary>
    /// Update a client about the current set of entries known to the coordinator
    /// </summary>
    /// <param name="data"></param>
    void Inform(string data);
    string Invoke(string path, string key, string payload, out bool failed);
}

#nullable disable

public class EntrySet
{
    public Dictionary<string, JToken> Entries { get; } = new();

    public string ToJson() => JsonConvert.SerializeObject(this);
    public static EntrySet FromJson(string s)
    {
        var result = JsonConvert.DeserializeObject<EntrySet>(s);
        result.Validate();
        return result;
    }

    private void Validate()
    {
        if (Entries is null)
        {
            throw new ArgumentNullException(nameof(Entries));
        }
        foreach (var source in Entries)
        {
            // steal validation from constructor
            _ = new CAddress(source.Key);
        }
    }
}

#nullable restore
