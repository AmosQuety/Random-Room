import { normalizeAnswer } from "../../lib/text";
import { BUILT_IN_COUNT, MAX_ITEMS, MIN_POOL, type BingoSetup } from "./types";

export const filledItems = (items: string[]): string[] => items.map((i) => i.trim()).filter(Boolean);

/** Custom items plus built-in ones that are not already among them, as the server counts them. Built-ins are only estimated. */
export function poolSize(setup: BingoSetup): number {
  return filledItems(setup.items).length + (setup.useBuiltIn ? BUILT_IN_COUNT : 0);
}

/** Mirrors the server's checks (accents and punctuation ignored), so the host hears about a problem on the setup step. */
export function bingoSetupIssue(setup: BingoSetup): string | null {
  const filled = filledItems(setup.items);
  if (new Set(filled.map(normalizeAnswer)).size !== filled.length) return "Each bingo item must be different.";
  if (filled.length > MAX_ITEMS) return `Use at most ${MAX_ITEMS} items.`;
  if (poolSize(setup) < MIN_POOL) return `Bingo needs at least ${MIN_POOL} different items, or turn on the built-in ones.`;
  return null;
}

export const isBingoSetupValid = (setup: BingoSetup): boolean => bingoSetupIssue(setup) === null;

export const toApiBingoSetup = (setup: BingoSetup) => ({ items: filledItems(setup.items), useBuiltIn: setup.useBuiltIn });
