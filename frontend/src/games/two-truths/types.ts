import type { RoundScore } from "../rounds/types";

export type TwoTruthsPhase = "lobby" | "submitting" | "voting" | "revealed" | "complete";

export interface TwoTruthsResult {
  storyteller: string;
  statements: string[];
  lie: number;
  votes: string[][];
  caught: string[];
  fooled: string[];
}

export interface TwoTruthsPayload {
  phase: TwoTruthsPhase;
  round: number;
  totalRounds: number;
  storyteller: string | null;
  /** Null until the storyteller has submitted. */
  statements: string[] | null;
  voted: Record<string, boolean>;
  myVote: number | null;
  /** Only ever set for the storyteller, until the reveal. */
  myLie: number | null;
  result: TwoTruthsResult | null;
  scoreboard: RoundScore[];
}

export type TwoTruthsSetup = Record<string, never>;
