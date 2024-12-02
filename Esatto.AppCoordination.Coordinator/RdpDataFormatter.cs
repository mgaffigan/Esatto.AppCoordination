using System.Text;

namespace Esatto.AppCoordination.Coordinator;

internal static class RdpDataFormatter
{
    public const int
        CMD_INFORM = 1,
        CMD_INVOKE_REQUEST = 2;

    public static byte[] CreateInvokeRequest(string? sourcePath, string path, string key, string payload)
    {
        sourcePath ??= "";
        var cbSourcePath = Encoding.UTF8.GetByteCount(sourcePath);
        var cbPath = Encoding.UTF8.GetByteCount(path);
        var cbKey = Encoding.UTF8.GetByteCount(key);
        var cbPayload = Encoding.UTF8.GetByteCount(payload);
        var data = new byte[4 * 4 /* Action + cbSourcePath + cbPath + cbKey */ + cbSourcePath + cbPath + cbKey + cbPayload];

        var i = 0;
        WriteInt32(data, i, CMD_INVOKE_REQUEST); i += 4;
        WriteInt32(data, i, cbSourcePath); i += 4;
        WriteInt32(data, i, cbPath); i += 4;
        WriteInt32(data, i, cbKey); i += 4;
        Encoding.UTF8.GetBytes(sourcePath, 0, cbSourcePath, data, i); i += cbSourcePath;
        Encoding.UTF8.GetBytes(path, 0, path.Length, data, i); i += cbPath;
        Encoding.UTF8.GetBytes(key, 0, key.Length, data, i); i += cbKey;
        Encoding.UTF8.GetBytes(payload, 0, payload.Length, data, i);
        return data;
    }

    public static (string? sourcePath, string path, string key, string payload) ReadInvokeRequest(byte[] data)
    {
        int i = 4 /* Header */;
        var cbSourcePath = ReadInt32(data, i); i += 4;
        var cbPath = ReadInt32(data, i); i += 4;
        var cbKey = ReadInt32(data, i); i += 4;
        var sourcePath = Encoding.UTF8.GetString(data, i, cbSourcePath); i += cbSourcePath;
        var path = Encoding.UTF8.GetString(data, i, cbPath); i += cbPath;
        var key = Encoding.UTF8.GetString(data, i, cbKey); i += cbKey;
        var payload = Encoding.UTF8.GetString(data, i, data.Length - i);

        if (sourcePath.Length == 0) sourcePath = null;

        return (sourcePath, path, key, payload);
    }

    public static byte[] CreateInformRequest(string sData)
    {
        var data = new byte[4 /* Action */ + Encoding.UTF8.GetByteCount(sData)];
        WriteInt32(data, 0, CMD_INFORM);
        Encoding.UTF8.GetBytes(sData, 0, sData.Length, data, 4 /* Header */);
        return data;
    }

    public static string ReadInformRequest(byte[] data)
    {
        return Encoding.UTF8.GetString(data, 4 /* Header */, data.Length - 4 /* Header */);
    }

    public static void WriteInt32(byte[] data, int index, int value)
    {
        data[index + 0] = (byte)((value >> 24) & 0xff);
        data[index + 1] = (byte)((value >> 16) & 0xff);
        data[index + 2] = (byte)((value >> 8) & 0xff);
        data[index + 3] = (byte)((value >> 0) & 0xff);
    }

    public static int ReadInt32(byte[] data, int index)
    {
        return (data[index + 0] << 24)
            | (data[index + 1] << 16)
            | (data[index + 2] << 8)
            | (data[index + 3] << 0);
    }
}
