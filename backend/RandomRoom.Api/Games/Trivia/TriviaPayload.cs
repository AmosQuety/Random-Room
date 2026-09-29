using RandomRoom.Api.Games.Shared;

namespace RandomRoom.Api.Games.Trivia;

public sealed record TriviaPreview(int QuestionCount);

public sealed record TriviaQuestionView(string Text, IReadOnlyList<string> Options, string? Category = null);

/// <summary>The just-finished question, shown alongside the next one so players see what they got right.</summary>
public sealed record TriviaReveal(string Text, IReadOnlyList<string> Options, int CorrectIndex, IReadOnlyDictionary<string, int> Answers);

public sealed record TriviaScore(string Player, int Correct);

public sealed record TriviaActivityView(Guid AnswerId, int QuestionNumber, string TriggeredBy, bool Correct, DateTimeOffset Timestamp);

public sealed record TriviaPayload(
    int QuestionNumber,
    int TotalQuestions,
    TriviaQuestionView? CurrentQuestion,
    IReadOnlyDictionary<string, bool> Answered,
    TriviaReveal? LastReveal,
    IReadOnlyList<TriviaScore> Scoreboard,
    IReadOnlyList<TriviaActivityView> Activity,
    TimerView? Timer = null,
    int? TimeLimitSeconds = null);
