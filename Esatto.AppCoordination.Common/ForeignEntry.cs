using Esatto.AppCoordination.IPC;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Esatto.AppCoordination;

public class ForeignEntry
{
    private readonly CoordinatedApp Parent;
    internal readonly CAddress Address;

    internal ForeignEntry(CoordinatedApp parent, CAddress address, JToken value)
    {
        this.Parent = parent;
        this.Address = address;
        this._Value = new EntryValue(value);
    }

    internal bool Update(JToken value)
    {
        if (JToken.DeepEquals(value, _Value.Value))
        {
            return false;
        }

        this._Value = new EntryValue(value);
        return true;
    }

    public override string ToString() => Address.ToString();

    public string Invoke(string payload) => Parent.Invoke(Address, payload);

    public string SourcePath => Address.Path;
    public string Key => Address.Key;

    private EntryValue _Value;
    public IReadOnlyEntryValue Value => _Value;

    internal void OnValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);
    public event EventHandler? ValueChanged;

    internal void OnRemoved() => Removed?.Invoke(this, EventArgs.Empty);
    public event EventHandler? Removed;
}
