import { normalizeAnswer } from "../../lib/text";
import { BOARD_SIZE, BUILT_IN_WORDS, MAX_WORDS, type SpySetup } from "./types";

export const filledWords = (words: string[]): string[] => words.map((w) => w.trim()).filter(Boolean);

/** Mirrors the server's checks (accents and punctuation ignored), so the host hears about a problem on the setup step. */
export function spySetupIssue(setup: SpySetup): string | null {
  const filled = filledWords(setup.words);
  if (new Set(filled.map(normalizeAnswer)).size !== filled.length) return "Each word must be different.";
  if (filled.length > MAX_WORDS) return `Use at most ${MAX_WORDS} words.`;
  const pool = filled.length + (setup.useBuiltIn ? BUILT_IN_WORDS : 0);
  if (pool < BOARD_SIZE) return `The board needs at least ${BOARD_SIZE} different words, or turn on the built-in ones.`;
  return null;
}

export const isSpySetupValid = (setup: SpySetup): boolean => spySetupIssue(setup) === null;

export const toApiSpySetup = (setup: SpySetup) => ({ words: filledWords(setup.words), useBuiltIn: setup.useBuiltIn });
