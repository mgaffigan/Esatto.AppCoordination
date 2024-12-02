using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Esatto.AppCoordination;

public class PublishedEntry : IDisposable
{
    bool isDisposed;

    internal PublishedEntry(PublishedEntryCollection parent, CAddress address, EntryValue value, Func<string, Task<string>>? action)
    {
        address.Validate();

        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Address = address;
        _Value = value ?? throw new ArgumentNullException(nameof(value));
        Action = action;
    }

    ~PublishedEntry()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool isDisposing)
    {
        if (isDisposed) return;
        isDisposed = true;

#if DEBUG
        if (!isDisposing)
        {
            Parent.Logger.LogInformation("Leaked PublishedEntry instance {Address}.  Did you forget to call Dispose()?", Address);
        }
#endif

        Parent.RemoveEntry(Address);

        if (isDisposing)
        {
            try
            {
                Disposed?.Invoke(this, EventArgs.Empty);
            }
            catch when (!isDisposing)
            {
                // nop
            }
        }
    }

    public event EventHandler? Disposed;

    public override string ToString() => Address.ToString();

    private readonly PublishedEntryCollection Parent;

    internal readonly Func<string, Task<string>>? Action;

    internal readonly CAddress Address;
    public string Key => Address.Key;
    public string Path => Address.Path;

    internal EntryValue _Value;
    public IReadOnlyEntryValue Value
    {
        get => _Value;
#if NET
        [MemberNotNull(nameof(_Value))]
#endif
        set
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(PublishedEntry));
            }

            var dup = value.Clone();
            if (_Value is not null && JsonNode.DeepEquals(_Value.Value, dup.Value))
            {
                return;
            }

            _Value = dup;
            Parent.UpdatePublished();
        }
    }
}