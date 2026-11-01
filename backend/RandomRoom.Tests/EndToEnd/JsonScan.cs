using System.Text.Json;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>Walks every value in a snapshot, so a secret is found wherever the server put it.</summary>
public static class JsonScan
{
    /// <summary>Every property in the tree, with a dotted path like "lastReveal.correctIndex".</summary>
    public static IEnumerable<(string Path, string Name, JsonElement Value)> Properties(JsonElement root, string path = "")
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in root.EnumerateObject())
                {
                    var here = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    yield return (here, property.Name, property.Value);
                    foreach (var inner in Properties(property.Value, here)) yield return inner;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                    foreach (var inner in Properties(item, path)) yield return inner;
                break;
        }
    }

    /// <summary>Every array made only of strings, wherever it sits.</summary>
    public static IEnumerable<string[]> StringArrays(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                var items = root.EnumerateArray().ToList();
                if (items.Count > 0 && items.All(i => i.ValueKind == JsonValueKind.String))
                    yield return items.Select(i => i.GetString()!).ToArray();
                foreach (var item in items)
                    foreach (var inner in StringArrays(item)) yield return inner;
                break;
            case JsonValueKind.Object:
                foreach (var property in root.EnumerateObject())
                    foreach (var inner in StringArrays(property.Value)) yield return inner;
                break;
        }
    }
}
