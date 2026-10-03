import type { PromptKit } from "../rounds/setup";

export interface IntroInput {
  clue: string;
  link: string;
  answers: string[];
}

export const MAX_ANSWERS = 6;

/** Mirrors the server: a link is optional, and when present it must be a full https:// address without credentials. */
export function isHttpsLink(value: string): boolean {
  try {
    const url = new URL(value.trim());
    return url.protocol === "https:" && url.username === "" && url.password === "";
  } catch {
    return false;
  }
}

const filledAnswers = (p: IntroInput) => p.answers.map((a) => a.trim()).filter(Boolean);

export const kit: PromptKit<IntroInput> = {
  builtInCount: 0,
  empty: () => ({ clue: "", link: "", answers: ["", ""] }),
  isFilled: (p) => p.clue.trim() !== "" || p.link.trim() !== "" || filledAnswers(p).length > 0,
  isValid: (p) =>
    p.clue.trim() !== "" && filledAnswers(p).length >= 1 && (p.link.trim() === "" || isHttpsLink(p.link)),
  problem: (p) => {
    if (p.clue.trim() === "") return "Give the clue some text.";
    if (filledAnswers(p).length < 1) return "Add at least one accepted answer.";
    return "The link must start with https://.";
  },
  toApi: (p) => ({
    clue: p.clue.trim(),
    ...(p.link.trim() ? { link: p.link.trim() } : {}),
    answers: filledAnswers(p),
  }),
};
