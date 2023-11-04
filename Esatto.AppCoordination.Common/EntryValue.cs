using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Esatto.AppCoordination;

public readonly struct JsonString : IEquatable<JsonString>
{
    public string Value { get; }
    public JsonString(string value)
    {
        this.Value = value;
    }

    #region Equality Boilerplate
    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override bool Equals(object? obj)
        => (obj is JsonString fea && Equals(fea))
        || (obj is string str && Equals((JsonString)str));
    public bool Equals(JsonString other) => Value == other.Value;

    public static explicit operator JsonString(string value) => new(value);
    public static explicit operator string(JsonString value) => value.Value;

    public static bool operator ==(JsonString left, JsonString right) => left.Equals(right);
    public static bool operator !=(JsonString left, JsonString right) => !(left == right);
    #endregion
}

public interface IReadOnlyEntryValue : IReadOnlyDictionary<string, object?>
{
    string JsonValue { get; }

    EntryValue Clone();
}

public class EntryValue : IDictionary<string, object?>, IReadOnlyEntryValue
{
    public string JsonValue
    {
        get => Value.ToString(Formatting.None);
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = JToken.Parse(value);
        }
    }

    internal EntryValue(JToken json)
    {
        this.Value = json;
    }

    public EntryValue()
    {
        Value = new JObject();
    }

    public EntryValue(string json)
    {
        Value = JToken.Parse(json);
    }

    public EntryValue Clone() => new EntryValue(Value.DeepClone());

    internal JToken Value;
    internal IDictionary<string, JToken?> Dictionary => Value as JObject
        ?? throw new InvalidOperationException("JsonValue is not object");

    private static object? JTokenToValue(JToken? token)
        => token is null ? null
        : token is JValue jv ? jv.Value
        : new JsonString(token.ToString(Formatting.None));

    private JToken ValueToJToken(object? value)
        // JToken.FromObject does not tolerate nulls
        => value is null ? JValue.CreateNull()
        : value is JsonString js ? JToken.Parse((string)js)
        : JToken.FromObject(value);

    #region Dictionary boilerplate
    public ICollection<string> Keys => Dictionary.Keys;
    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => Dictionary.Keys;
    public ICollection<object?> Values => Dictionary.Values.Select(JTokenToValue).ToList();
    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => Dictionary.Values.Select(JTokenToValue);
    public int Count => Dictionary.Count;
    public bool IsReadOnly => false;

    public object? this[string key]
    {
        get => JTokenToValue(Dictionary[key]);
        set => Dictionary[key] = ValueToJToken(value);
    }

    public bool ContainsKey(string key) => Dictionary.ContainsKey(key);
    public void Add(string key, object? value) => Dictionary.Add(key, ValueToJToken(value));
    public bool Remove(string key) => Dictionary.Remove(key);

    public bool TryGetValue(string key, out object? value)
    {
        if (Dictionary.TryGetValue(key, out var jtv))
        {
            value = JTokenToValue(jtv);
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }

    public void Add(KeyValuePair<string, object?> item) => Dictionary.Add(item.Key, ValueToJToken(item));

    public void Clear() => Dictionary.Clear();

    public bool Contains(KeyValuePair<string, object?> item) => Dictionary.Contains(new(item.Key, ValueToJToken(item.Value)));

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        foreach (var kvp in Dictionary)
        {
            array[arrayIndex++] = new(kvp.Key, JTokenToValue(kvp.Value));
        }
    }

    public bool Remove(KeyValuePair<string, object?> item) => Dictionary.Remove(item.Key);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return Dictionary
            .Select(kvp => new KeyValuePair<string, object?>(kvp.Key, JTokenToValue(kvp.Value)))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion
}