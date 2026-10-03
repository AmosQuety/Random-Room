import { normalizeAnswer } from "../../lib/text";
import { MAX_CUSTOM_WORDS, MAX_WORD_LENGTH, type SketchSetup } from "./types";

export const filledWords = (words: string[]): string[] => words.map((w) => w.trim()).filter(Boolean);

/** Mirrors the server's checks, so the host hears about a problem word on the setup step and not after Create. */
export function sketchSetupIssue(setup: SketchSetup): string | null {
  const filled = filledWords(setup.words);
  const empty = filled.findIndex((w) => normalizeAnswer(w) === "");
  if (empty >= 0) return `Word ${empty + 1} needs letters or numbers.`;
  const tooLong = filled.findIndex((w) => w.length > MAX_WORD_LENGTH);
  if (tooLong >= 0) return `Word ${tooLong + 1} is too long (at most ${MAX_WORD_LENGTH} characters).`;
  if (new Set(filled.map(normalizeAnswer)).size !== filled.length) return "Each word must be different.";
  if (filled.length > MAX_CUSTOM_WORDS) return `Use at most ${MAX_CUSTOM_WORDS} words.`;
  if (!setup.useBuiltIn && filled.length < 1) return "Add at least one word, or turn on the built-in ones.";
  return null;
}

export const isSketchSetupValid = (setup: SketchSetup): boolean => sketchSetupIssue(setup) === null;

export const toApiSketchSetup = (setup: SketchSetup) => ({
  words: filledWords(setup.words),
  useBuiltIn: setup.useBuiltIn,
  rounds: setup.rounds,
  timeLimitSeconds: setup.timeLimitSeconds,
});
