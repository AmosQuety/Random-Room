namespace RandomRoom.Api.Domain;

public sealed record Choice(string Name, int Age);

/// <summary>The single hardcoded game this MVP serves. No room creation, no editing.</summary>
public static class RoomDefinition
{
    public const string Slug = "who-do-we-choose";
    public const string Name = "Who do we choose?";

    public static readonly IReadOnlyList<Choice> Choices =
    [
        new("Sarah", 17),
        new("Judith", 25),
    ];

    public static readonly IReadOnlyList<string> Players = ["Amos", "Lydia", "James", "Jacob"];

    public static bool IsPlayer(string name) => Players.Contains(name, StringComparer.Ordinal);
}
