using System.Globalization;
using System.Text;

namespace RandomRoom.Api.Games.Shared;

/// <summary>Makes free-text answers comparable: case, accents, punctuation and spacing don't matter.</summary>
public static class AnswerNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
            else if (!lastWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }
        return builder.ToString().Trim();
    }

    public static bool MatchesAny(string? guess, IEnumerable<string> accepted)
    {
        var normalized = Normalize(guess);
        return normalized.Length > 0 && accepted.Any(a => Normalize(a) == normalized);
    }
}
