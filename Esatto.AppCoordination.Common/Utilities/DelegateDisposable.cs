namespace Esatto.AppCoordination;

internal sealed class DelegateDisposable(Action dispose) : IDisposable
{
    private Action? _Dispose = dispose;

    public void Dispose()
    {
        var dispose = _Dispose;
        _Dispose = null;
        dispose?.Invoke();
    }
}