import type { GameModule } from "../types";
import { TriviaGameScreen } from "./GameScreen";
import { TriviaSetupForm } from "./SetupForm";
import type { TriviaPayload, TriviaSetup } from "./types";

function isQuestionValid(q: TriviaSetup[number]): boolean {
  const options = q.options.map((o) => o.trim()).filter(Boolean);
  return q.text.trim().length > 0 && options.length >= 2 && q.correctIndex >= 0 && q.correctIndex < options.length;
}

export const triviaModule: GameModule<TriviaSetup, TriviaPayload> = {
  key: "trivia",
  name: "Trivia",
  description: "Host-curated multiple-choice questions. Everyone answers, then the correct one reveals.",
  defaultSetup: [{ text: "", options: ["", ""], correctIndex: 0 }],
  isSetupValid: (setup) => setup.length >= 1 && setup.every(isQuestionValid),
  toApiSetup: (setup) => ({
    questions: setup.map((q) => ({
      text: q.text.trim(),
      options: q.options.map((o) => o.trim()).filter(Boolean),
      correctIndex: q.correctIndex,
    })),
  }),
  SetupForm: TriviaSetupForm,
  GameScreen: TriviaGameScreen,
};
