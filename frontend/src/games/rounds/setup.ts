/** Setup shared by every prompt-and-answer game: built-in prompts, the host's own, how many rounds, and a time limit. */
export interface RoundSetupState<TInput> {
  useBuiltIn: boolean;
  custom: TInput[];
  rounds: number;
  /** Seconds per round, or null for no limit. */
  timeLimit: number | null;
}

/** What a game says about its own prompts; the setup logic is the same for all of them. */
export interface PromptKit<TInput> {
  /** How many ready-made prompts the server ships for this game (0 when there are none). */
  builtInCount: number;
  empty: () => TInput;
  /** Anything typed at all. Untouched rows are ignored rather than treated as mistakes. */
  isFilled: (prompt: TInput) => boolean;
  isValid: (prompt: TInput) => boolean;
  /** What is wrong with a prompt the host started but did not finish. A generic line is used when absent. */
  problem?: (prompt: TInput) => string | null;
  toApi: (prompt: TInput) => unknown;
}

export const ROUND_CHOICES = [3, 5, 8, 10, 15, 20, 30];
export const TIME_LIMIT_CHOICES = [15, 30, 60, 90];

export function filledPrompts<TInput>(setup: RoundSetupState<TInput>, kit: PromptKit<TInput>): TInput[] {
  return setup.custom.filter(kit.isFilled);
}

export function availablePromptCount<TInput>(setup: RoundSetupState<TInput>, kit: PromptKit<TInput>): number {
  return (setup.useBuiltIn ? kit.builtInCount : 0) + filledPrompts(setup, kit).length;
}

export function effectiveRounds<TInput>(setup: RoundSetupState<TInput>, kit: PromptKit<TInput>): number {
  return Math.max(1, Math.min(setup.rounds, availablePromptCount(setup, kit)));
}

/** The round counts to offer: the usual ones that fit, plus "all of them" and whatever is currently selected. */
export function roundOptions(total: number, selected: number): number[] {
  const fitting = ROUND_CHOICES.filter((n) => n < total);
  return [...new Set([...fitting, total, selected])].filter((n) => n >= 1).sort((a, b) => a - b);
}

export function roundSetupIssue<TInput>(setup: RoundSetupState<TInput>, kit: PromptKit<TInput>): string | null {
  const unfinished = setup.custom.filter(kit.isFilled).find((p) => !kit.isValid(p));
  if (unfinished) return kit.problem?.(unfinished) ?? "Finish the prompt you started, or clear it.";
  if (availablePromptCount(setup, kit) < 1) return "Add at least one prompt, or turn on the built-in ones.";
  return null;
}

export function roundSetupFor<TInput>(kit: PromptKit<TInput>) {
  return {
    defaultSetup: {
      useBuiltIn: kit.builtInCount > 0,
      custom: kit.builtInCount > 0 ? [] : [kit.empty()],
      rounds: 8,
      timeLimit: null,
    } as RoundSetupState<TInput>,

    isSetupValid: (setup: RoundSetupState<TInput>) =>
      availablePromptCount(setup, kit) >= 1 && setup.custom.filter(kit.isFilled).every(kit.isValid),

    setupIssue: (setup: RoundSetupState<TInput>) => roundSetupIssue(setup, kit),

    toApiSetup: (setup: RoundSetupState<TInput>) => ({
      useBuiltIn: setup.useBuiltIn,
      prompts: filledPrompts(setup, kit).map(kit.toApi),
      rounds: effectiveRounds(setup, kit),
      ...(setup.timeLimit ? { timeLimitSeconds: setup.timeLimit } : {}),
    }),
  };
}
