using System.Runtime.InteropServices;

namespace Esatto.AppCoordination.Coordinator;

internal static class ComExceptionExtensions
{
    public static bool IsServerDisconnected(this COMException ex)
    {
        return ex.HResult == unchecked((int)0x80010108) /* RPC_E_DISCONNECTED */
            || ex.HResult == unchecked((int)0x800706BA) /* RPC_S_SERVER_UNAVAILABLE */;
    }
}
