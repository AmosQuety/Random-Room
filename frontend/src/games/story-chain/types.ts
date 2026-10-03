import type { RoundScore } from "../rounds/types";

export type ChainPhase = "lobby" | "writing" | "complete";

export interface ChainEntry {
  player: string;
  text: string;
  /** The lead-in the server added for that turn, such as "Fortunately,". Empty when the game has none. */
  prefix: string;
}

export interface ChainPayload {
  phase: ChainPhase;
  opener: string;
  entries: ChainEntry[];
  turn: number;
  totalTurns: number;
  currentPlayer: string | null;
  prefix: string | null;
  scoreboard: RoundScore[];
}

export interface ChainSetup {
  openers: string[];
  useBuiltIn: boolean;
  length: number;
}

/** What differs between the story games on screen. The rules themselves live on the server. */
export interface ChainConfig {
  /** "word" or "sentence": what one turn contributes. */
  piece: string;
  lengthLabel: string;
  lengthChoices: number[];
  defaultLength: number;
  maxChars: number;
  /** True when a turn is a single word, so entries join with a space and no lead-in is shown. */
  singleWord: boolean;
  inputHint: string;
  intro: string;
}

export const MAX_OPENERS = 30;
export const BUILT_IN_OPENERS = 30;
