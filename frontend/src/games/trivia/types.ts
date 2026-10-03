import type { TimerView } from "../rounds/types";

export interface TriviaQuestionView {
  text: string;
  options: string[];
  category?: string | null;
}

/** The just-finished question, shown alongside the next one so players see what they got right. */
export interface TriviaReveal {
  text: string;
  options: string[];
  correctIndex: number;
  answers: Record<string, number>;
}

export interface TriviaScore {
  player: string;
  correct: number;
}

export interface TriviaActivityView {
  answerId: string;
  questionNumber: number;
  triggeredBy: string;
  correct: boolean;
  timestamp: string;
}

export interface TriviaPayload {
  questionNumber: number;
  totalQuestions: number;
  currentQuestion: TriviaQuestionView | null;
  answered: Record<string, boolean>;
  lastReveal: TriviaReveal | null;
  scoreboard: TriviaScore[];
  activity: TriviaActivityView[];
  /** Present only when the host set a time limit. */
  timer?: TimerView | null;
  timeLimitSeconds?: number | null;
}

export interface TriviaPreview {
  questionCount: number;
}

export interface TriviaQuestionInput {
  text: string;
  options: string[];
  correctIndex: number;
  category: string;
}

/** Local setup-form state. */
export interface TriviaSetup {
  questions: TriviaQuestionInput[];
  useBuiltIn: boolean;
  builtInCount: number;
  /** Seconds per question, or null for no limit. */
  timeLimit: number | null;
}
