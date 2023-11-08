namespace Esatto.AppCoordination.Teleport;

class TeleportDepthScope : IDisposable
{
    public int Depth { get; }

    public TeleportDepthScope()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable(TeleportConstants.TeleportDepth), out int result))
        {
            Depth = result;
        }
        Environment.SetEnvironmentVariable(TeleportConstants.TeleportDepth, (Depth + 1).ToString());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(TeleportConstants.TeleportDepth, Depth.ToString());
    }
}
