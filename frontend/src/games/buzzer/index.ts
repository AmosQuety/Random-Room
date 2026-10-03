import { lazy } from "react";
import type { GameModule } from "../types";
import { BuzzerGlyph } from "./Glyph";
import { isBuzzerSetupValid, toApiBuzzerSetup } from "./setup";
import type { BuzzerPayload, BuzzerSetup } from "./types";

export const buzzerModule: GameModule<BuzzerSetup, BuzzerPayload> = {
  key: "buzzer",
  name: "Buzzer Round",
  description: "The host reads a prompt and opens the buzzers. The first to buzz answers out loud and the host judges. The server decides who was first.",
  hook: "Fastest finger wins.",
  category: "reflex",
  accent: "mustard",
  Glyph: BuzzerGlyph,
  minPlayers: 3,
  maxPlayers: 12,
  defaultSetup: { prompts: [""], useBuiltIn: true, rounds: 8 },
  isSetupValid: isBuzzerSetupValid,
  toApiSetup: toApiBuzzerSetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.BuzzerSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.BuzzerGameScreen }))),
};
