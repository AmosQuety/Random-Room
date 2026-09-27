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

    /// <summary>
    /// Validates and persists this game's room-level setup (e.g. Random Picker's choices,
    /// Trivia's question bank). Called once, at room creation. Throws RoomRuleException.InvalidInput
    /// for a malformed or insufficient setup.
    /// </summary>
    Task ConfigureRoomAsync(Guid roomId, JsonElement setup, CancellationToken ct);

    /// <summary>Called right after a new session row is created, to set up any game-specific state for it.</summary>
    Task OnSessionCreatedAsync(Guid roomId, Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Applies one player action (e.g. "trigger" for Random Picker, "answer" for trivia) to an active session.
    /// Throws RoomRuleException for anything the game's own rules forbid.
    /// </summary>
    Task HandleActionAsync(Guid roomId, Guid sessionId, string actor, string action, JsonElement? payload, CancellationToken ct);

    /// <summary>Whether the session's win condition has been met (e.g. every player has gone).</summary>
    Task<bool> IsSessionCompleteAsync(Guid sessionId, CancellationToken ct);

    /// <summary>The game-specific slice of the room snapshot - whatever shape this game needs to render.</summary>
    Task<object> GetPayloadAsync(Guid roomId, Guid sessionId, CancellationToken ct);

    /// <summary>A teaser shown on the public join screen, before anyone has authenticated - never answers/results.</summary>
    Task<object> GetRoomPreviewAsync(Guid roomId, CancellationToken ct);
}
