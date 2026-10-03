import type { PromptKit } from "../rounds/setup";

export interface CardInput {
  word: string;
  /** Comma separated, as typed. */
  forbidden: string;
}

export const MAX_FORBIDDEN = 8;
export const MAX_WORD = 30;

export const splitForbidden = (text: string): string[] => text.split(",").map((f) => f.trim()).filter(Boolean);

const norm = (s: string) => s.toLowerCase().replace(/[^\p{L}\p{N}]/gu, "");

export function cardProblem(card: CardInput): string | null {
  const forbidden = splitForbidden(card.forbidden);
  if (card.word.trim() === "") return "Give the card a word.";
  if (forbidden.length === 0) return "Add at least one forbidden word, separated by commas.";
  if (forbidden.length > MAX_FORBIDDEN) return `At most ${MAX_FORBIDDEN} forbidden words.`;
  if (forbidden.some((f) => f.length > MAX_WORD)) return `Forbidden words can be at most ${MAX_WORD} characters.`;
  if (forbidden.some((f) => norm(f) === norm(card.word))) return "A card cannot forbid its own word.";
  return null;
}

export const kit: PromptKit<CardInput> = {
  builtInCount: 40,
  empty: () => ({ word: "", forbidden: "" }),
  isFilled: (c) => c.word.trim() !== "" || c.forbidden.trim() !== "",
  isValid: (c) => cardProblem(c) === null,
  problem: cardProblem,
  toApi: (c) => ({ word: c.word.trim(), forbidden: splitForbidden(c.forbidden) }),
};
