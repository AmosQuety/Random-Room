import { lazy } from "react";
import type { GameModule } from "../types";
import { TriviaGlyph } from "./Glyph";
import { defaultSetup, isSetupValid, setupIssue, toApiSetup } from "./setup";
import type { TriviaPayload, TriviaSetup } from "./types";

export const triviaModule: GameModule<TriviaSetup, TriviaPayload> = {
  key: "trivia",
  name: "Trivia",
  description: "Multiple-choice questions, one at a time. Write your own, mix in the built-in starter set, and optionally put every question on a clock.",
  hook: "Multiple choice, one point each. Know it or lose it.",
  category: "quiz",
  accent: "sky",
  Glyph: TriviaGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup,
  isSetupValid,
  setupIssue,
  toApiSetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.TriviaSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.TriviaGameScreen }))),
};
