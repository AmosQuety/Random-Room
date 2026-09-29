import type { PromptKit } from "./setup";
import type { RoundPayload } from "./types";

/** Two options, everyone picks one privately: shared by Would You Rather and This or That. */
export interface TwoWayInput {
  a: string;
  b: string;
}

export interface TwoWayPromptView {
  options: [string, string];
}

export interface TwoWayResult {
  options: [string, string];
  voters: [string[], string[]];
  counts: [number, number];
  majority: 0 | 1 | null;
}

export interface TwoWayAnswer {
  choice: 0 | 1;
}

export type TwoWayPayload = RoundPayload<TwoWayPromptView, TwoWayResult, TwoWayAnswer>;

export function twoWayKit(builtInCount: number): PromptKit<TwoWayInput> {
  return {
    builtInCount,
    empty: () => ({ a: "", b: "" }),
    isFilled: (p) => p.a.trim() !== "" || p.b.trim() !== "",
    isValid: (p) => p.a.trim() !== "" && p.b.trim() !== "",
    toApi: (p) => ({ a: p.a.trim(), b: p.b.trim() }),
  };
}
