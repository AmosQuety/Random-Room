using System.Text.Json;
using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Shared;

/// <summary>Reads a player's raw answer at the boundary, turning a wrong shape into a friendly InvalidInput.</summary>
public static class AnswerJson
{
    public static int RequiredInt(JsonElement raw, string name)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The answer needs a whole number called '{name}'.");
        return value;
    }

    public static bool RequiredBool(JsonElement raw, string name)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty(name, out var element) || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The answer needs true or false called '{name}'.");
        return element.GetBoolean();
    }

    public static string RequiredText(JsonElement raw, string name, int maxLength)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The answer needs text called '{name}'.");

        var text = element.GetString()!.Trim();
        if (text.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "The answer cannot be empty.");
        if (text.Length > maxLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"The answer is too long (at most {maxLength} characters).");
        return text;
    }
}
