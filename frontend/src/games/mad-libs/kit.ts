import type { PromptKit } from "../rounds/setup";

export interface StoryInput {
  title: string;
  text: string;
}

export const MIN_BLANKS = 2;
export const MAX_BLANKS = 12;
export const MAX_LABEL = 24;
export const MAX_WORD = 30;

/** The word types written as {noun} in a story, in order. Mirrors the server's parser. */
export function blankLabels(text: string): string[] {
  return [...text.matchAll(/\{([^{}\r\n]*)\}/g)].map((m) => m[1].trim());
}

export function storyProblem(story: StoryInput): string | null {
  const labels = blankLabels(story.text);
  if (story.title.trim() === "") return "Give the story a title.";
  if (labels.length < MIN_BLANKS || labels.length > MAX_BLANKS) return `Use ${MIN_BLANKS} to ${MAX_BLANKS} blanks, written like {noun}. You have ${labels.length}.`;
  if (labels.some((l) => l === "" || l.length > MAX_LABEL)) return `Each blank needs a word type of up to ${MAX_LABEL} characters, like {adjective}.`;
  return null;
}

export const kit: PromptKit<StoryInput> = {
  builtInCount: 30,
  empty: () => ({ title: "", text: "" }),
  isFilled: (s) => s.title.trim() !== "" || s.text.trim() !== "",
  isValid: (s) => storyProblem(s) === null,
  toApi: (s) => ({ title: s.title.trim(), text: s.text.trim() }),
};
