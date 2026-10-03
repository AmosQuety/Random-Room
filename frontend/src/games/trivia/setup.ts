import type { TriviaQuestionInput, TriviaSetup } from "./types";

/** Matches the number of starter questions the server ships (backend Games/Content/trivia-starter.json). */
export const STARTER_COUNT = 30;
export const STARTER_COUNT_CHOICES = [5, 10, 15, 20, 30];
export const TIME_LIMIT_CHOICES = [10, 15, 20, 30, 45, 60];

/** These match the server's limits (TriviaEngine), so a question the form accepts is never refused after the last step. */
export const MAX_QUESTIONS = 50;
export const MAX_QUESTION_LENGTH = 300;
export const MAX_OPTIONS = 6;
export const MAX_OPTION_LENGTH = 100;
export const MAX_CATEGORY_LENGTH = 40;

export const emptyQuestion = (): TriviaQuestionInput => ({ text: "", options: ["", ""], correctIndex: 0, category: "" });

export const defaultSetup: TriviaSetup = { questions: [emptyQuestion()], useBuiltIn: false, builtInCount: 10, timeLimit: null };

/** Untouched rows are ignored rather than treated as mistakes. */
export const isFilled = (q: TriviaQuestionInput): boolean =>
  q.text.trim() !== "" || q.options.some((o) => o.trim() !== "");

export function isQuestionValid(q: TriviaQuestionInput): boolean {
  const options = q.options.map((o) => o.trim()).filter(Boolean);
  return q.text.trim().length > 0 && options.length >= 2 && q.correctIndex >= 0 && q.correctIndex < options.length && q.category.trim().length <= MAX_CATEGORY_LENGTH
    && q.text.trim().length <= MAX_QUESTION_LENGTH && q.options.length <= MAX_OPTIONS && q.options.every((o) => o.trim().length <= MAX_OPTION_LENGTH);
}

export const questionCount = (setup: TriviaSetup): number =>
  setup.questions.filter(isFilled).length + (setup.useBuiltIn ? setup.builtInCount : 0);

/** What is wrong with the setup, in the host's words, or null when it is ready. */
export function setupIssue(setup: TriviaSetup): string | null {
  const filled = setup.questions.filter(isFilled);
  const unfinished = filled.findIndex((q) => !isQuestionValid(q));
  if (unfinished >= 0) return `Question ${unfinished + 1} needs a question, at least two options and a correct answer.`;
  const total = questionCount(setup);
  if (total < 1) return "Add at least one question, or turn on the starter set.";
  if (total > MAX_QUESTIONS) return `Use at most ${MAX_QUESTIONS} questions, yours and the starter ones together.`;
  return null;
}

export const isSetupValid = (setup: TriviaSetup): boolean => setupIssue(setup) === null;

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
