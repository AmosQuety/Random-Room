import type { RoundScore } from "../rounds/types";

export type Team = "red" | "blue";
export type Owner = Team | "neutral" | "assassin";
export type SpyPhase = "lobby" | "clue" | "guessing" | "complete";

export interface SpyCard {
  word: string;
  /** Present only once the card is turned over, or the game is over. */
  owner: Owner | null;
}

export interface SpyClue {
  word: string;
  count: number;
  by: string;
}

export interface SpyPayload {
  phase: SpyPhase;
  board: SpyCard[];
  teams: Record<Team, string[]>;
  spymasters: Partial<Record<Team, string>>;
  myTeam: Team | null;
  isSpymaster: boolean;
  /** The whole key: only for a spymaster, or for everyone once the game is over. */
  key: Owner[] | null;
  turn: Team | null;
  clue: SpyClue | null;
  guessesLeft: number;
  remaining: Partial<Record<Team, number>>;
  winner: Team | null;
  endReason: "assassin" | "all-found" | "ended" | null;
  clueLog: SpyClue[];
  scoreboard: RoundScore[];
}

export interface SpySetup {
  words: string[];
  useBuiltIn: boolean;
}

export const MAX_WORDS = 100;
export const MAX_WORD_LENGTH = 24;
export const BUILT_IN_WORDS = 132;
export const BOARD_SIZE = 25;
export const MAX_CLUE_COUNT = 9;

export const teamName = (team: Team): string => (team === "red" ? "Red" : "Blue");
