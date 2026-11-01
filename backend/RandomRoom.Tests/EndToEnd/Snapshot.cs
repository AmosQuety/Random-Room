using System.Text.Json;

namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// One room snapshot exactly as a client received it. Kept as JSON so strategies read what a real client reads,
/// and so the secret scan sees every field the server sent.
/// </summary>
public sealed class Snapshot(JsonElement raw)
{
    public JsonElement Raw { get; } = raw.Clone();

    public long Sequence => Raw.GetProperty("sequence").GetInt64();

    public string Status => Raw.GetProperty("session").GetProperty("status").GetString()!;

    public int SessionNumber => Raw.GetProperty("session").GetProperty("number").GetInt32();

    public JsonElement Payload => Raw.GetProperty("gamePayload");

    public bool IsCompleted => Status == "Completed";
}
