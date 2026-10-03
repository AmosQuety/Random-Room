import { lazy } from "react";
import type { GameModule } from "../types";
import { WordSpiesGlyph } from "./Glyph";
import { isSpySetupValid, toApiSpySetup } from "./setup";
import type { SpyPayload, SpySetup } from "./types";

export const wordSpiesModule: GameModule<SpySetup, SpyPayload> = {
  key: "word-spies",
  name: "Word Spies",
  description: "Two teams race to find their words on a grid. Only each team's spymaster sees the key, and gives one-word clues. Avoid the assassin.",
  hook: "One-word clues. One assassin.",
  category: "word",
  accent: "plum",
  Glyph: WordSpiesGlyph,
  minPlayers: 4,
  maxPlayers: 12,
  defaultSetup: { words: [""], useBuiltIn: true },
  isSetupValid: isSpySetupValid,
  toApiSetup: toApiSpySetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.WordSpiesSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.WordSpiesGameScreen }))),
};
