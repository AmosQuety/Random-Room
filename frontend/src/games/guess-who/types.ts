import type { RoundScore } from "../rounds/types";

export type GuessWhoPhase = "lobby" | "submitting" | "voting" | "revealed" | "complete";

export interface GuessWhoResult {
  author: string;
  text: string;
  guesses: { player: string; guessed: string }[];
  correct: string[];
}

export interface GuessWhoPayload {
  phase: GuessWhoPhase;
  round: number;
  totalRounds: number;
  /** The fact being guessed. Never says who wrote it. */
  text: string | null;
  /** True only for the fact's own author. */
  isMine: boolean;
  /** Who has submitted a fact. Only meaningful while facts are being collected. */
  submitted: Record<string, boolean>;
  myFact: string | null;
  guessCount: number;
  guessesNeeded: number;
  myGuess: string | null;
  result: GuessWhoResult | null;
  scoreboard: RoundScore[];
}

export type GuessWhoSetup = Record<string, never>;
