import type { RoundScore } from "./types";

/** Players with the top score, or nobody while everyone is on zero. */
export function winnersOf(scoreboard: RoundScore[]): string[] {
  const top = Math.max(0, ...scoreboard.map((s) => s.score));
  return top === 0 ? [] : scoreboard.filter((s) => s.score === top).map((s) => s.player);
}
