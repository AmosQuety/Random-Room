using System.Text.Json;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Shared;

/// <summary>Reads host-supplied setup documents at the boundary, turning anything malformed into a friendly InvalidInput.</summary>
public static class SetupJson
{
    public static string RequiredText(JsonElement source, string name, int maxLength, string label)
    {
        var text = OptionalText(source, name, maxLength, label);
        if (text.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} cannot be empty.");
        return text;
    }

    public static string OptionalText(JsonElement source, string name, int maxLength, string label)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return "";
        if (element.ValueKind != JsonValueKind.String)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} must be text.");

        var text = element.GetString()!.Trim();
        if (text.Length > maxLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} is too long (at most {maxLength} characters).");
        return text;
    }

    public static bool Flag(JsonElement source, string name, bool fallback)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var element)) return fallback;
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => fallback,
            _ => throw new RoomRuleException(RuleViolation.InvalidInput, $"'{name}' must be true or false."),
        };
    }

    public static int? OptionalInt(JsonElement source, string name, int min, int max, string label)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var number) || number < min || number > max)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} must be a whole number from {min} to {max}.");
        return number;
    }

    public static IReadOnlyList<JsonElement> OptionalArray(JsonElement source, string name, int maxItems, string label)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return [];
        if (element.ValueKind != JsonValueKind.Array)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} must be a list.");

        var items = element.EnumerateArray().ToList();
        if (items.Count > maxItems)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"{label} can have at most {maxItems} entries.");
        return items;
    }
}
