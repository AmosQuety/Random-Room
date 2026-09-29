import { BOARD_SIZE, BUILT_IN_WORDS, MAX_WORDS, type SpySetup } from "./types";

export const filledWords = (words: string[]): string[] => words.map((w) => w.trim()).filter(Boolean);

export function isSpySetupValid(setup: SpySetup): boolean {
  const filled = filledWords(setup.words);
  const distinct = new Set(filled.map((w) => w.toLowerCase()));
  const pool = filled.length + (setup.useBuiltIn ? BUILT_IN_WORDS : 0);
  return distinct.size === filled.length && filled.length <= MAX_WORDS && pool >= BOARD_SIZE;
}

export const toApiSpySetup = (setup: SpySetup) => ({ words: filledWords(setup.words), useBuiltIn: setup.useBuiltIn });
