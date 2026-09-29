import type { RoundScore } from "../rounds/types";

export type BingoPhase = "lobby" | "calling" | "complete";

export interface BingoPayload {
  phase: BingoPhase;
  /** Items called so far, oldest first. */
  called: string[];
  poolSize: number;
  /** The viewer's own 25 cells, row by row; the free centre is an empty string. Null when they have no card. */
  myCard: string[] | null;
  myMarks: boolean[] | null;
  winner: string | null;
  winningLine: number[] | null;
  scoreboard: RoundScore[];
}

export interface BingoSetup {
  items: string[];
  useBuiltIn: boolean;
}

export const MAX_ITEMS = 75;
export const BUILT_IN_COUNT = 40;
/** A card holds 24 items plus the free centre; one more in the pool lets cards differ. */
export const MIN_POOL = 25;
export const FREE_CELL = 12;
