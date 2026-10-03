import type { RoundScore } from "../rounds/types";

export type BuzzerPhase = "lobby" | "waiting" | "open" | "resolved" | "complete";

export interface BuzzerPayload {
  phase: BuzzerPhase;
  round: number;
  totalRounds: number;
  prompt: string | null;
  /** Who has buzzed and is waiting to be judged. */
  buzzed: string | null;
  lockedOut: string[];
  winner: string | null;
  scoreboard: RoundScore[];
}

export interface BuzzerSetup {
  prompts: string[];
  useBuiltIn: boolean;
  rounds: number;
}

export const MAX_PROMPTS = 30;
export const BUILT_IN_COUNT = 30;
export const ROUND_CHOICES = [3, 5, 8, 10, 15, 20];
