import { lazy } from "react";
import type { GameModule } from "../types";
import { TwoTruthsGlyph } from "./Glyph";
import type { TwoTruthsPayload, TwoTruthsSetup } from "./types";

export const twoTruthsModule: GameModule<TwoTruthsSetup, TwoTruthsPayload> = {
  key: "two-truths",
  name: "Two Truths and a Lie",
  description: "Each player takes a turn: two true statements about themselves and one lie. Everyone else votes on the lie.",
  hook: "Fool your friends with a straight face.",
  category: "quiz",
  accent: "plum",
  Glyph: TwoTruthsGlyph,
  minPlayers: 3,
  maxPlayers: 12,
  defaultSetup: {},
  isSetupValid: () => true,
  toApiSetup: () => ({}),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.TwoTruthsSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.TwoTruthsGameScreen }))),
};
