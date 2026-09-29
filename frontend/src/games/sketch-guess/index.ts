import { lazy } from "react";
import type { GameModule } from "../types";
import { SketchGuessGlyph } from "./Glyph";
import { isSketchSetupValid, toApiSketchSetup } from "./setup";
import type { SketchPayload, SketchSetup } from "./types";

export const sketchGuessModule: GameModule<SketchSetup, SketchPayload> = {
  key: "sketch-guess",
  name: "Sketch Guess",
  description: "One player draws a secret word on a shared canvas while everyone else races to guess it. Only the drawer ever sees the word.",
  hook: "Draw it. Guess it.",
  category: "word",
  accent: "sky",
  Glyph: SketchGuessGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: { words: [""], useBuiltIn: true, rounds: 6, timeLimitSeconds: 90 },
  isSetupValid: isSketchSetupValid,
  toApiSetup: toApiSketchSetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.SketchGuessSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.SketchGuessGameScreen }))),
};
