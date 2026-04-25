using System.Globalization;

namespace Esatto.AppCoordination.Teleport;

class TeleportDepthScope : IDisposable
{
    public int PriorDepth { get; }

    public TeleportDepthScope(int? depth = null)
    {
        this.PriorDepth = depth ?? GetCurrentDepth();
        Environment.SetEnvironmentVariable(TeleportConstants.TeleportDepth, (PriorDepth + 1).ToString(CultureInfo.InvariantCulture));
    }

    public static int GetCurrentDepth()
    {
        var value = Environment.GetEnvironmentVariable(TeleportConstants.TeleportDepth);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            && result >= 0 ? result : 0;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(TeleportConstants.TeleportDepth, PriorDepth.ToString(CultureInfo.InvariantCulture));
    }
}
