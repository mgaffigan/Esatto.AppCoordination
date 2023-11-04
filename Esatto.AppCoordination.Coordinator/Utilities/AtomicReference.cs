namespace Esatto.AppCoordination.Coordinator;

internal class AtomicReference<T>
    where T : class
{
    private readonly object SyncRoot = new();
    private T _Value;
    public T Value
    {
        get
        {
            lock (SyncRoot)
            {
                return _Value;
            }
        }
        set
        {
            lock (SyncRoot)
            {
                _Value = value;
            }
        }
    }

    public AtomicReference(T value)
    {
        this._Value = value;
    }
}