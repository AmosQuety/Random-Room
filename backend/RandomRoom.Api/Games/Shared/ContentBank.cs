using System.Collections.Concurrent;
using System.Text.Json;

namespace RandomRoom.Api.Games.Shared;

/// <summary>
/// Built-in content (prompts, words, templates) shipped as versioned JSON embedded in the assembly:
/// { "version": 1, "items": [ ... ] }. Embedded rather than read from disk so it works the same in tests,
/// in the Docker image and under any working directory.
/// </summary>
public static class ContentBank
{
    private sealed record Document<T>(int Version, List<T> Items);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, object> Cache = new();

    public static IReadOnlyList<T> Load<T>(string name) =>
        (IReadOnlyList<T>)Cache.GetOrAdd($"{typeof(T).FullName}:{name}", _ => Read<T>(name));

    private static IReadOnlyList<T> Read<T>(string name)
    {
        using var stream = typeof(ContentBank).Assembly.GetManifestResourceStream($"Content.{name}.json")
            ?? throw new InvalidOperationException($"Content bank '{name}' is missing.");
        var document = JsonSerializer.Deserialize<Document<T>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Content bank '{name}' is empty.");
        return document.Items;
    }
}
