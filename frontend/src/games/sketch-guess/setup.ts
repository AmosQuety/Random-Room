import { MAX_CUSTOM_WORDS, type SketchSetup } from "./types";

export const filledWords = (words: string[]): string[] => words.map((w) => w.trim()).filter(Boolean);

export function isSketchSetupValid(setup: SketchSetup): boolean {
  const filled = filledWords(setup.words);
  const distinct = new Set(filled.map((w) => w.toLowerCase()));
  if (distinct.size !== filled.length || filled.length > MAX_CUSTOM_WORDS) return false;
  return setup.useBuiltIn || filled.length >= 1;
}

export const toApiSketchSetup = (setup: SketchSetup) => ({
  words: filledWords(setup.words),
  useBuiltIn: setup.useBuiltIn,
  rounds: setup.rounds,
  timeLimitSeconds: setup.timeLimitSeconds,
});
