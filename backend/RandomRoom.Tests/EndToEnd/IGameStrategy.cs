namespace RandomRoom.Tests.EndToEnd;

/// <summary>
/// How to play one game type through the real API, and what counts as a secret in it. A strategy reads only the
/// snapshots its bots received, so a game that leaks fails here rather than being played with inside knowledge.
/// Adding a game means adding a strategy; Bot, GameTable and GamePlay do not change.
/// </summary>
public interface IGameStrategy
{
    string GameType { get; }

    /// <summary>The setup the room is created with.</summary>
    object Setup(int playerCount);

    /// <summary>
    /// Plays one unit of the game for every bot (a round, a question, one call) and returns once every connected bot
    /// has seen the result. Returns true when the game is over.
    /// </summary>
    Task<bool> PlayStepAsync(GameTable table);

    /// <summary>Describes every secret a bot was sent that it should not have had. Empty means none.</summary>
    IReadOnlyList<string> FindLeaks(GameTable table);
}
