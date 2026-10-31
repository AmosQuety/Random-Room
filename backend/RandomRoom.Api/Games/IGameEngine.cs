using System.Text.Json;

namespace RandomRoom.Api.Games;

/// <summary>
/// One pluggable game type. GameSessionService owns the generic room/session lifecycle
/// (who's the host, is the session active, when does a new session start); everything
/// about what happens inside an active session belongs to the engine for that GameType.
/// </summary>
public interface IGameEngine
{
    /// <summary>Matches Room.GameType. Used to pick this engine out of the registered set.</summary>
    string GameType { get; }

    /// <summary>The fewest players this game works with. Rooms hold 2 to 12 players; a game may narrow that.</summary>
    int MinPlayers => 2;

    /// <summary>The most players this game works with.</summary>
    int MaxPlayers => 12;

    /// <summary>
    /// The fewest participants when the host referees. Players are always counted as participants, so this differs
    /// from MinPlayers only for a game where a host who plays still does not take part in play (Buzzer).
    /// </summary>
    int MinPlayersWithReferee => MinPlayers;

    /// <summary>
    /// Whether the host always referees this game, whatever the room's HostPlays says (Buzzer: the host judges). Such a
    /// game refuses a request for the host to play.
    /// </summary>
    bool RequiresReferee => false;

    /// <summary>
    /// The actions a referee host may send: running the game (reveal, next, end, award, judge), never playing it.
    /// Checked centrally before HandleActionAsync. Empty by default, so a referee can do nothing a game has not allowed.
    /// </summary>
    IReadOnlySet<string> RefereeActions => EmptyActions;

    private static readonly IReadOnlySet<string> EmptyActions = new HashSet<string>();

    /// <summary>
    /// Validates and persists this game's setup for one session (e.g. Random Picker's choices, Trivia's question
    /// bank). Called at room creation, for the first session; a new game of the same kind copies the previous
    /// session's setup instead. Throws RoomRuleException.InvalidInput for a malformed or insufficient setup.
    /// </summary>
    Task ConfigureSessionAsync(Guid roomId, Guid sessionId, JsonElement setup, CancellationToken ct);

    /// <summary>Called right after a new session row is created, to set up any game-specific state for it.</summary>
    Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Called when a session becomes Active (the host started it, or a new round began): set deadlines and move
    /// the game out of its lobby phase. Additive: engines with nothing to do can ignore it.
    /// </summary>
    Task OnSessionStartedAsync(Guid roomId, Guid sessionId, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Applies one player action (e.g. "trigger" for Random Picker, "answer" for trivia) to an active session.
    /// Throws RoomRuleException for anything the game's own rules forbid.
    /// </summary>
    Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct);

    /// <summary>Whether the session's win condition has been met (e.g. every player has gone).</summary>
    Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct);

    /// <summary>The game-specific slice of the room snapshot - whatever shape this game needs to render.</summary>
    Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct);

    /// <summary>
    /// The payload as one specific player may see it. Games with hidden information (private answers, a secret
    /// word, a card only some players hold) override this and must strip anything the viewer is not entitled to.
    /// Defaults to the shared payload, which for such games must itself be the safe public view.
    /// </summary>
    Task<object> GetPayloadForAsync(Guid roomId, Guid sessionId, string viewer, CancellationToken ct) =>
        GetPayloadAsync(roomId, sessionId, ct);

    /// <summary>A teaser shown on the public join screen, before anyone has authenticated - never answers/results.</summary>
    Task<object> GetRoomPreviewAsync(Guid roomId, Guid sessionId, CancellationToken ct);
}
