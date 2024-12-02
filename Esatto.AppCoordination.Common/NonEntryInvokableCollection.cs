#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Esatto.AppCoordination;

internal delegate void NonEntryDelegate(string? sourcePath, string path, string key, string payload);

internal sealed class NonEntryInvokableCollection
{
    private readonly object SyncNonEntryDelegates = new();
    private readonly List<NonEntryInvokable> NonEntryDelegates = new();

    internal struct NonEntryInvokable(string prefix, NonEntryDelegate action)
    {
        public string Prefix { get; } = prefix;
        public NonEntryDelegate Action { get; } = action;
    }

    public IDisposable Add(string prefix, NonEntryDelegate action)
    {
        CPath.Validate(prefix);

        var invokable = new NonEntryInvokable(prefix, action);
        lock (SyncNonEntryDelegates) NonEntryDelegates.Add(invokable);

        return new DelegateDisposable(() =>
        {
            lock (SyncNonEntryDelegates) NonEntryDelegates.Remove(invokable);
        });
    }

#if NET
    public bool TryGet(string path, [MaybeNullWhen(false)] out NonEntryDelegate action)
#else
    public bool TryGet(string path, out NonEntryDelegate action)
#endif
    {
        lock (SyncNonEntryDelegates)
        {
            action = NonEntryDelegates.FirstOrDefault(i => path.StartsWith(i.Prefix, StringComparison.Ordinal)).Action;
            return action != null;
        }
    }
}
