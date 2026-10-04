/** What every prompt-and-answer game (see backend Games/Rounds) sends: the same envelope, game-specific prompt and result. */
export type RoundPhase = "lobby" | "collecting" | "revealed" | "complete";

export interface TimerView {
  deadlineAt: string | null;
  serverNow: string;
}

/** A score change (or a decision not to change one) the host made, shown to everyone so a host who plays cannot tilt a score unseen. */
export interface HostScoreNote {
  round: number;
  player: string;
  points: number;
  reason: string;
}

export interface RoundScore {
  player: string;
  score: number;
}

export interface RoundPayload<TPrompt, TResult, TAnswer> {
  phase: RoundPhase;
  round: number;
  totalRounds: number;
  prompt: TPrompt | null;
  /** Who has answered, never what. */
  answered: Record<string, boolean>;
  /** The viewer's own answer, or null. */
  myAnswer: TAnswer | null;
  /** Present only once the round is revealed. */
  result: TResult | null;
  scoreboard: RoundScore[];
  timer: TimerView;
  timeLimitSeconds: number | null;
}

export interface RoundPreview {
  promptCount: number;
  rounds: number;
  timeLimitSeconds: number | null;
}
