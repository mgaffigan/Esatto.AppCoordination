using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

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

public static class IReadOnlyEntryValueExtensions
{
#if NET
    [return: NotNullIfNotNull(nameof(@default))]
#endif
    public static T? GetValueOrDefault<T>(this IReadOnlyEntryValue @this, string key, T? @default = default)
    {
        if (@this.TryGetValue(key, out var oValue) && oValue is not null)
        {
            try
            {
                var t = typeof(T);
                t = Nullable.GetUnderlyingType(t) ?? t;
                return (T)Convert.ChangeType(oValue, t)!;
            }
            catch
            {
                // nop if wrong datatype
            }
        }
        return @default;
    }
}

public class EntryValue : IDictionary<string, object?>, IReadOnlyEntryValue
{
    public string JsonValue
    {
        get => Value.ToJsonString(CoordinationConstants.JsonSerializerOptions);
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = JsonNode.Parse(value)!;
        }
    }

    internal EntryValue(JsonNode json)
    {
        this.Value = json;
    }

    public EntryValue()
    {
        Value = new JsonObject();
    }

    public EntryValue(string json)
    {
        Value = JsonNode.Parse(json)!;
    }

    public EntryValue Clone() => new EntryValue(Value.DeepClone());

    internal JsonNode Value;
    internal IDictionary<string, JsonNode?> Dictionary => Value as JsonObject
        ?? throw new InvalidOperationException("JsonValue is not object");

    private static object? JTokenToValue(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonValue jv)
        {
            switch (jv.GetValueKind())
            {
                case JsonValueKind.String: 
                    return jv.GetValue<string>();

                case JsonValueKind.True or JsonValueKind.False:
                    return jv.GetValue<bool>();

                case JsonValueKind.Number:
                    var d = jv.GetValue<double>();
                    if (d == (int)d) return (int)d;
                    return d;
            };
        }

        return new JsonString(node.ToJsonString(CoordinationConstants.JsonSerializerOptions));
    }

    private JsonNode? ValueToJToken(object? value)
        => value is null ? null
        : value is JsonString js ? JsonNode.Parse((string)js)
        : JsonSerializer.SerializeToNode(value, CoordinationConstants.JsonSerializerOptions);

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

    public void Add(KeyValuePair<string, object?> item) => Dictionary.Add(item.Key, ValueToJToken(item.Value));

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
