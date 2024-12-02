using Esatto.Utilities;

namespace Esatto.AppCoordination;

internal sealed class AggregateDisposable : IDisposable
{
    private readonly IDisposable[] Disposables;

    public AggregateDisposable(params IDisposable[] disposables)
    {
        this.Disposables = disposables;
    }

    public void Dispose() => Disposables.DisposeAll();
}
