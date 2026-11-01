using RandomRoom.Api.Services;

namespace RandomRoom.Api.Games.Shared;

/// <summary>A game's player range, applied to participants: a referee host is not counted.</summary>
public static class ParticipantRange
{
    public static void Require(IGameEngine engine, int participants, bool hostPlays)
    {
        var min = hostPlays ? engine.MinPlayers : engine.MinPlayersWithReferee;
        var notCounting = hostPlays ? "" : ", not counting the host";
        if (participants < min)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"This game needs at least {min} players{notCounting}.");
        if (participants > engine.MaxPlayers)
            throw new RoomRuleException(RuleViolation.InvalidInput, $"This game works with at most {engine.MaxPlayers} players{notCounting}.");
    }
}
