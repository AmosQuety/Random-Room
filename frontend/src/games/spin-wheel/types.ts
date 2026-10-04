import type { HostScoreNote, RoundScore } from "../rounds/types";

export type WheelPhase = "lobby" | "ready" | "spun" | "complete";

export interface WheelSpin {
  player: string;
  index: number;
  label: string;
}

export interface WheelPayload {
  phase: WheelPhase;
  round: number;
  totalRounds: number;
  segments: string[];
  spinner: string | null;
  last: WheelSpin | null;
  awarded: boolean;
  scoreboard: RoundScore[];
  /** Who gives the point: the host, or (when the host is the one spinning) any other player. Never the spinner. */
  awardMode?: "host" | "players";
  hostScoring?: HostScoreNote[];
}

export interface WheelSetup {
  segments: string[];
  useBuiltIn: boolean;
  spins: number;
}

export const MIN_SEGMENTS = 2;
export const MAX_SEGMENTS = 12;
export const BUILT_IN_COUNT = 30;
export const SPIN_CHOICES = [3, 6, 9, 12, 18, 24];
