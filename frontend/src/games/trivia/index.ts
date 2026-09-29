import { lazy } from "react";
import type { GameModule } from "../types";
import { TriviaGlyph } from "./Glyph";
import type { TriviaPayload, TriviaSetup } from "./types";

function isQuestionValid(q: TriviaSetup[number]): boolean {
  const options = q.options.map((o) => o.trim()).filter(Boolean);
  return q.text.trim().length > 0 && options.length >= 2 && q.correctIndex >= 0 && q.correctIndex < options.length;
}

export const triviaModule: GameModule<TriviaSetup, TriviaPayload> = {
  key: "trivia",
  name: "Trivia",
  description: "Host-curated multiple-choice questions. Everyone answers, then the correct one reveals.",
  hook: "Multiple choice, one point each. Know it or lose it.",
  category: "quiz",
  accent: "sky",
  Glyph: TriviaGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: [{ text: "", options: ["", ""], correctIndex: 0 }],
  isSetupValid: (setup) => setup.length >= 1 && setup.every(isQuestionValid),
  toApiSetup: (setup) => ({
    questions: setup.map((q) => ({
      text: q.text.trim(),
      options: q.options.map((o) => o.trim()).filter(Boolean),
      correctIndex: q.correctIndex,
    })),
  }),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.TriviaSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.TriviaGameScreen }))),
};
