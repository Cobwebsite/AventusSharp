using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AventusSharp.AspNetCore.Hosting;

internal sealed class HeaderDictionary : IDictionary<string, string[]>
{
    private readonly IHeaderDictionary headers;

    public HeaderDictionary(IHeaderDictionary headers) => this.headers = headers;

    public string[] this[string key]
    {
        get => ToArray(headers[key]);
        set => headers[key] = new StringValues(value);
    }

    public ICollection<string> Keys => headers.Keys;
    public ICollection<string[]> Values => headers.Values.Select(ToArray).ToArray();
    public int Count => headers.Count;
    public bool IsReadOnly => false;

    public void Add(string key, string[] value)
    {
        if (headers.ContainsKey(key))
        {
            throw new ArgumentException($"A header named '{key}' already exists.", nameof(key));
        }
        headers[key] = new StringValues(value);
    }
    public void Add(KeyValuePair<string, string[]> item) => Add(item.Key, item.Value);
    public void Clear() => headers.Clear();
    public bool Contains(KeyValuePair<string, string[]> item) =>
        TryGetValue(item.Key, out string[]? value) && value.SequenceEqual(item.Value);
    public bool ContainsKey(string key) => headers.ContainsKey(key);
    public void CopyTo(KeyValuePair<string, string[]>[] array, int arrayIndex)
    {
        foreach (KeyValuePair<string, string[]> item in this)
        {
            array[arrayIndex++] = item;
        }
    }
    public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator() =>
        headers.Select(item => new KeyValuePair<string, string[]>(item.Key, ToArray(item.Value))).GetEnumerator();
    public bool Remove(string key) => headers.Remove(key);
    public bool Remove(KeyValuePair<string, string[]> item) => Contains(item) && Remove(item.Key);
    public bool TryGetValue(string key, out string[] value)
    {
        if (headers.TryGetValue(key, out StringValues found))
        {
            value = ToArray(found);
            return true;
        }
        value = Array.Empty<string>();
        return false;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string[] ToArray(StringValues values) =>
        values.Select(value => value ?? string.Empty).ToArray();
}
