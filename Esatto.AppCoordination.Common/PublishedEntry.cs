using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Esatto.AppCoordination;

public class PublishedEntry : IDisposable
{
    internal PublishedEntry(PublishedEntryCollection parent, CAddress address, EntryValue value, Func<string, string>? action)
    {
        Parent = parent;
        Address = address;
        _Value = value;
        Action = action;
    }
    
    ~PublishedEntry()
    {
        Dispose();
    }

    public void Dispose()
    {
        Parent.RemoveEntry(Address);
    }

    public override string ToString() => Address.ToString();

    private readonly PublishedEntryCollection Parent;

    internal readonly Func<string, string>? Action;

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
            var dup = value.Clone();
            if (_Value is not null && JToken.DeepEquals(_Value.Value, dup.Value))
            {
                return;
            }

            _Value = dup;
            Parent.UpdatePublished();
        }
    }
}