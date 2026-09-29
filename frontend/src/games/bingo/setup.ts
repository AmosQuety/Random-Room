import { BUILT_IN_COUNT, MAX_ITEMS, MIN_POOL, type BingoSetup } from "./types";

export const filledItems = (items: string[]): string[] => items.map((i) => i.trim()).filter(Boolean);

/** Custom items plus built-in ones that are not already among them, as the server counts them. Built-ins are only estimated. */
export function poolSize(setup: BingoSetup): number {
  return filledItems(setup.items).length + (setup.useBuiltIn ? BUILT_IN_COUNT : 0);
}

export function isBingoSetupValid(setup: BingoSetup): boolean {
  const filled = filledItems(setup.items);
  const distinct = new Set(filled.map((i) => i.toLowerCase()));
  return distinct.size === filled.length && filled.length <= MAX_ITEMS && poolSize(setup) >= MIN_POOL;
}

export const toApiBingoSetup = (setup: BingoSetup) => ({ items: filledItems(setup.items), useBuiltIn: setup.useBuiltIn });
