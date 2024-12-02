using Esatto.Utilities;
using System.Text.Json.Nodes;

namespace Esatto.AppCoordination;

public class ForeignEntry
{
    private readonly CoordinatedApp Parent;
    internal readonly CAddress Address;
    private readonly List<PublishedEntry> Dependents = new();

    internal ForeignEntry(CoordinatedApp parent, CAddress address, EntryValue value)
    {
        this.Parent = parent;
        this.Address = address;
        this._Value = value;
    }

    internal bool Update(JsonNode value)
    {
        if (JsonNode.DeepEquals(value, _Value.Value))
        {
            return false;
        }

        this._Value = new EntryValue(value);
        return true;
    }

    public override string ToString() => Address.ToString();

    public Task<string> InvokeAsync(string payload, CancellationToken ct = default)
        => Parent.InvokeAsync(Address, payload, ct);

    public void InvokeOneWay(string payload)
        => Parent.InvokeOneWay(Address, payload);

    public string SourcePath => Address.Path;
    public string Key => Address.Key;

    public string DisplayName => Value.GetValueOrDefault("DisplayName", Key)!;

    private EntryValue _Value;
    public IReadOnlyEntryValue Value => _Value;

    internal void OnValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
    public event EventHandler? ValueChanged;

    internal void OnRemoved()
    {
        lock (Dependents)
        {
            Dependents.DisposeAll();
        }
        Removed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Removed;

    public PublishedEntry PublishDependent(string key, EntryValue value)
        => PublishDependent(key, value, (Func<string, Task<string>>)null!);
    public PublishedEntry PublishDependent(string key, EntryValue value, Func<string, string> action)
        => PublishDependent(key, value, k => Task.FromResult(action(k)));

    public PublishedEntry PublishDependent(string key, EntryValue value, Func<string, Task<string>> action)
    {
        var ent = Parent.Publish(key, value, action);
        lock (Dependents)
        {
            Dependents.Add(ent);
        }
        return ent;
    }
}
