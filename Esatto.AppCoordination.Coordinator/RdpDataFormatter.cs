using System.Text;

namespace Esatto.AppCoordination.Coordinator;

internal static class RdpDataFormatter
{
    public const int
        CMD_INFORM = 1,
        CMD_INVOKE_REQUEST = 2,
        CMD_INVOKE_RESPONSE_RESULT = 3,
        CMD_INVOKE_RESPONSE_ERROR = 4;

    public static byte[] CreateInvokeRequest(int correlation, string path, string key, string payload)
    {
        var szPath = Encoding.UTF8.GetByteCount(path);
        var szKey = Encoding.UTF8.GetByteCount(key);
        var szPayload = Encoding.UTF8.GetByteCount(payload);
        var data = new byte[4 * 4 /* Action + Correlation + szPath + szKey */ + szPath + szKey + szPayload];

        var i = 0;
        WriteInt32(data, i, CMD_INVOKE_REQUEST); i += 4;
        WriteInt32(data, i, correlation); i += 4;
        WriteInt32(data, i, szPath); i += 4;
        WriteInt32(data, i, szKey); i += 4;
        Encoding.UTF8.GetBytes(path, 0, path.Length, data, i); i += szPath;
        Encoding.UTF8.GetBytes(key, 0, key.Length, data, i); i += szKey;
        Encoding.UTF8.GetBytes(payload, 0, payload.Length, data, i);
        return data;
    }

    public static (int correlation, string path, string key, string payload) ReadInvokeRequest(byte[] data)
    {
        int i = 4 /* Header */;
        var correlation = ReadInt32(data, i); i += 4;
        var szPath = ReadInt32(data, i); i += 4;
        var szKey = ReadInt32(data, i); i += 4;
        var path = Encoding.UTF8.GetString(data, i, szPath); i += szPath;
        var key = Encoding.UTF8.GetString(data, i, szKey); i += szKey;
        var payload = Encoding.UTF8.GetString(data, i, data.Length - i);
        return (correlation, path, key, payload);
    }

    public static byte[] CreateInvokeResponse(int type, int correlation, string payload)
    {
        var data = new byte[8 /* Action + Correlation */ + Encoding.UTF8.GetByteCount(payload)];
        int i = 0;
        WriteInt32(data, i, type); i += 4;
        WriteInt32(data, i, correlation); i += 4;
        Encoding.UTF8.GetBytes(payload, 0, payload.Length, data, i);
        return data;
    }

    public static (int correlation, string payload) ReadInvokeResponse(byte[] data)
    {
        int i = 4 /* Action */;
        var correlation = ReadInt32(data, i); i += 4;
        var payload = Encoding.UTF8.GetString(data, i, data.Length - i);
        return (correlation, payload);
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
