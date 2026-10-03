import { lazy } from "react";
import type { GameModule } from "../types";
import { RandomPickerGlyph } from "./Glyph";
import type { RandomPickerPayload, RandomPickerSetup } from "./types";

export const randomPickerModule: GameModule<RandomPickerSetup, RandomPickerPayload> = {
  key: "random-picker",
  name: "Random Picker",
  description: "Everyone triggers a server-random pick from a shared list. Nobody chooses for themselves.",
  hook: "The server picks for you. No arguing.",
  category: "reflex",
  accent: "tomato",
  Glyph: RandomPickerGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: ["", ""],
  // The server keeps only distinct, non-empty choices and needs two of them, so count them the same way.
  isSetupValid: (setup) => new Set(setup.map((c) => c.trim()).filter(Boolean)).size >= 2,
  toApiSetup: (setup) => ({ choices: setup.map((c) => c.trim()).filter(Boolean) }),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.RandomPickerSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.RandomPickerGameScreen }))),
};
