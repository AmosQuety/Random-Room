import type { TriviaQuestionInput, TriviaSetup } from "./types";

/** Matches the number of starter questions the server ships (backend Games/Content/trivia-starter.json). */
export const STARTER_COUNT = 30;
export const STARTER_COUNT_CHOICES = [5, 10, 15, 20, 30];
export const TIME_LIMIT_CHOICES = [10, 15, 20, 30, 45, 60];

export const emptyQuestion = (): TriviaQuestionInput => ({ text: "", options: ["", ""], correctIndex: 0, category: "" });

export const defaultSetup: TriviaSetup = { questions: [emptyQuestion()], useBuiltIn: false, builtInCount: 10, timeLimit: null };

/** Untouched rows are ignored rather than treated as mistakes. */
export const isFilled = (q: TriviaQuestionInput): boolean =>
  q.text.trim() !== "" || q.options.some((o) => o.trim() !== "");

export function isQuestionValid(q: TriviaQuestionInput): boolean {
  const options = q.options.map((o) => o.trim()).filter(Boolean);
  return q.text.trim().length > 0 && options.length >= 2 && q.correctIndex >= 0 && q.correctIndex < options.length && q.category.trim().length <= 40;
}

export function isSetupValid(setup: TriviaSetup): boolean {
  const filled = setup.questions.filter(isFilled);
  return filled.every(isQuestionValid) && filled.length + (setup.useBuiltIn ? setup.builtInCount : 0) >= 1;
}

export function toApiSetup(setup: TriviaSetup) {
  return {
    questions: setup.questions.filter(isFilled).map((q) => ({
      text: q.text.trim(),
      options: q.options.map((o) => o.trim()).filter(Boolean),
      correctIndex: q.correctIndex,
      ...(q.category.trim() ? { category: q.category.trim() } : {}),
    })),
    ...(setup.useBuiltIn ? { useBuiltIn: true, builtInCount: setup.builtInCount } : {}),
    ...(setup.timeLimit ? { timeLimitSeconds: setup.timeLimit } : {}),
  };
}
