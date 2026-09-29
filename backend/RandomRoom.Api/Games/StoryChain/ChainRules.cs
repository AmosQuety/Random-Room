using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.StoryChain;

/// <summary>One word per turn. A word is a single run of characters with no spaces, up to 24 long, with a letter or digit in it.</summary>
public sealed class OneWordRules : IChainRules
{
    public const int MaxWordLength = 24;

    public string GameType => "one-word-story";
    public string OpenerBank => "one-word-story";
    public int MinLength => 10;
    public int MaxLength => 100;
    public int DefaultLength => 40;

    public string PrefixFor(int turn) => "";

    public string Validate(string text)
    {
        var word = text.Trim();
        if (word.Any(char.IsWhiteSpace))
            throw new RoomRuleException(RuleViolation.InvalidInput, "Just one word per turn.");
        if (word.Length > MaxWordLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Keep it to {MaxWordLength} characters or fewer.");
        if (!word.Any(char.IsLetterOrDigit))
            throw new RoomRuleException(RuleViolation.InvalidInput, "A word needs at least one letter or number.");
        return word;
    }
}

/// <summary>
/// Turns alternate. The server adds "Fortunately," on even turns and "Unfortunately," on odd ones, so a player writes
/// only what follows. A player who types the lead-in themselves has it removed rather than doubled.
/// </summary>
public sealed class FortunatelyRules : IChainRules
{
    public const int MaxSentenceLength = 140;
    public const string Good = "Fortunately,";
    public const string Bad = "Unfortunately,";

    public string GameType => "fortunately";
    public string OpenerBank => "fortunately";
    public int MinLength => 4;
    public int MaxLength => 30;
    public int DefaultLength => 12;

    public string PrefixFor(int turn) => turn % 2 == 0 ? Good : Bad;

    public string Validate(string text)
    {
        var sentence = StripLeadIn(text.Trim());
        if (sentence.Length == 0)
            throw new RoomRuleException(RuleViolation.InvalidInput, "Write what happens next.");
        if (sentence.Length > MaxSentenceLength)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"Keep it to {MaxSentenceLength} characters or fewer.");
        return sentence;
    }

    private static string StripLeadIn(string text)
    {
        foreach (var lead in new[] { Bad, Good, "Unfortunately", "Fortunately" })
        {
            if (text.StartsWith(lead, StringComparison.OrdinalIgnoreCase))
                return text[lead.Length..].TrimStart(' ', ',');
        }
        return text;
    }
}
