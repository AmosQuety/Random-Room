namespace RandomRoom.Api.Games.Rounds;

public static class RoundGameRegistration
{
    /// <summary>Registers a prompt-and-answer game: its rules, wrapped in the shared round engine.</summary>
    public static IServiceCollection AddRoundGame<TPrompt, TRules>(this IServiceCollection services)
        where TRules : class, IRoundRules<TPrompt>, new()
    {
        services.AddScoped<IGameEngine>(sp => new RoundGameEngine<TPrompt>(
            new TRules(),
            sp.GetRequiredService<Shared.GameStore>(),
            sp.GetRequiredService<Services.IRandomChoiceSource>(),
            sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
