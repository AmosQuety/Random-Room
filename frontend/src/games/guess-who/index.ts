import { lazy } from "react";
import type { GameModule } from "../types";
import { GuessWhoGlyph } from "./Glyph";
import type { GuessWhoPayload, GuessWhoSetup } from "./types";

export const guessWhoModule: GameModule<GuessWhoSetup, GuessWhoPayload> = {
  key: "guess-who",
  name: "Guess Who Wrote It",
  description: "Everyone writes a fact about themselves. The facts come up one at a time and the group guesses who wrote each.",
  hook: "Who secretly climbed Kilimanjaro?",
  category: "quiz",
  accent: "leaf",
  Glyph: GuessWhoGlyph,
  minPlayers: 3,
  maxPlayers: 12,
  defaultSetup: {},
  isSetupValid: () => true,
  toApiSetup: () => ({}),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.GuessWhoSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.GuessWhoGameScreen }))),
};
