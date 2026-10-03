import type { RoundScore, TimerView } from "../rounds/types";

export type ForbiddenPhase = "lobby" | "playing" | "revealed" | "complete";
export type ForbiddenRole = "describer" | "judge" | "guesser";
export type Outcome = "guessed" | "flagged" | "skipped" | "time" | "ended";

export interface CardView {
  word: string;
  forbidden: string[];
}

export interface GuessView {
  player: string;
  text: string;
}

export interface ForbiddenResult {
  outcome: Outcome;
  guesser: string | null;
  word: string;
  forbidden: string[];
}

export interface ForbiddenPayload {
  phase: ForbiddenPhase;
  round: number;
  totalRounds: number;
  describer: string | null;
  judge: string | null;
  role: ForbiddenRole | null;
  /** Only the describer and the judge ever receive it. */
  card: CardView | null;
  guesses: GuessView[];
  myGuessesLeft: number;
  result: ForbiddenResult | null;
  timer: TimerView;
  timeLimitSeconds: number | null;
  scoreboard: RoundScore[];
}

export const OUTCOME_TEXT: Record<Outcome, string> = {
  guessed: "guessed it",
  flagged: "slipped: a forbidden word was said",
  skipped: "skipped the card",
  time: "ran out of time",
  ended: "ended by the host",
};
