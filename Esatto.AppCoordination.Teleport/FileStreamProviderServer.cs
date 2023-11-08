using Esatto.Utilities;

namespace Esatto.AppCoordination.Teleport;

internal sealed class FileStreamProviderServer : IDisposable
{
    private readonly FileStreamProvider? Prov;
    private readonly ContextAwareCoalescingAction? CA;
    private readonly ThreadAssert threadAssert = new();
    private bool isStarted;
    private bool isShutdown;

    public FileStreamProviderServer(FileStreamProvider? prov)
    {
        this.Prov = prov;
        if (prov is null)
        {
            isShutdown = true;
        }
        else
        {
            CA = new ContextAwareCoalescingAction(Shutdown,
                TeleportSettings.Instance.MaxReadInterval,
                TeleportSettings.Instance.MaxReadTime,
                SynchronizationContext.Current);

            prov.Disposed += (_, _) => Shutdown();
            prov.Touched += (_, _) => CA?.Set();
        }
    }

    public void Dispose() => CA?.Dispose();

    private void Shutdown()
    {
        threadAssert.Assert();

        if (isShutdown) return;
        isShutdown = true;

        if (isStarted)
        {
            // prov is disposed by parent
            Application.Exit();
        }
    }

    public void Run()
    {
        threadAssert.Assert();

        if (isShutdown) return;

        isStarted = true;
        Application.Run();
    }
}
