import type { RoundScore, TimerView } from "../rounds/types";

export type SketchPhase = "lobby" | "drawing" | "revealed" | "complete";
export type SketchOutcome = "guessed" | "skipped" | "time" | "ended";

/** Points are flattened x,y pairs on a 0..1000 grid; color indexes PALETTE. */
export interface Stroke {
  color: number;
  size: number;
  points: number[];
}

export interface SketchGuess {
  player: string;
  text: string;
}

export interface SketchResult {
  outcome: SketchOutcome;
  guesser: string | null;
  word: string;
}

export interface SketchPayload {
  phase: SketchPhase;
  round: number;
  totalRounds: number;
  drawer: string | null;
  /** Only the drawer ever receives it, and only while the round is on. */
  word: string | null;
  strokes: Stroke[];
  guesses: SketchGuess[];
  myGuessesLeft: number;
  result: SketchResult | null;
  timer: TimerView;
  timeLimitSeconds: number | null;
  scoreboard: RoundScore[];
}

export interface SketchSetup {
  words: string[];
  useBuiltIn: boolean;
  rounds: number;
  timeLimitSeconds: number;
}

export const MAX_CUSTOM_WORDS = 60;
export const MAX_WORD_LENGTH = 30;
export const BUILT_IN_COUNT = 105;
export const ROUND_CHOICES = [3, 6, 9, 12, 18, 24];
export const TIME_CHOICES = [45, 60, 90, 120, 180];

export const OUTCOME_TEXT: Record<SketchOutcome, string> = {
  guessed: "guessed it",
  skipped: "skipped the word",
  time: "ran out of time",
  ended: "ended by the host",
};
