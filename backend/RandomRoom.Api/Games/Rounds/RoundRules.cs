using System.Text.Json;

namespace RandomRoom.Api.Games.Rounds;

/// <summary>One player's stored answer for the round being resolved.</summary>
public sealed record RoundAnswer(string Player, JsonElement Value);

/// <summary>What a revealed round produces: a game-specific summary for the screen, and points per player.</summary>
public sealed record RoundOutcome(object Summary, IReadOnlyDictionary<string, int> Points);

/// <summary>
/// The parts of a prompt-and-answer game that differ between games. Everything else (phases, answers hidden
/// until the reveal, one answer per player, deadlines, scoring totals, recovery after a restart) lives once in
/// RoundGameEngine, which is composed with one of these.
/// </summary>
public interface IRoundRules<TPrompt>
{
    string GameType { get; }

    int MinPlayers => 2;

    int MaxPlayers => 12;

    /// <summary>Name of the embedded content bank holding the built-in prompts, or null when the game has none.</summary>
    string? BuiltInBank { get; }

    /// <summary>Validates one host-written prompt from the setup form. Throws InvalidInput.</summary>
    TPrompt ParsePrompt(JsonElement raw);

    /// <summary>The prompt as players see it while answering. Must never contain the answer key.</summary>
    object ViewPrompt(TPrompt prompt);

    /// <summary>Validates one player's raw answer and returns the normalized form to store. Throws InvalidInput.</summary>
    JsonElement ValidateAnswer(TPrompt prompt, string actor, IReadOnlyList<string> players, JsonElement raw);

    /// <summary>Turns every stored answer into the reveal summary and the points earned.</summary>
    RoundOutcome Resolve(TPrompt prompt, IReadOnlyList<RoundAnswer> answers, IReadOnlyList<string> players);
}

/// <summary>The room-level setup every round game stores: which prompts, how many rounds, and an optional time limit.</summary>
public sealed record RoundSetup<TPrompt>(List<TPrompt> Prompts, int Rounds, int? TimeLimitSeconds);
