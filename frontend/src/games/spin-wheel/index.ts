import { lazy } from "react";
import type { GameModule } from "../types";
import { SpinWheelGlyph } from "./Glyph";
import { wheelSetupIssue, isWheelSetupValid, toApiWheelSetup } from "./setup";
import type { WheelPayload, WheelSetup } from "./types";

export const spinWheelModule: GameModule<WheelSetup, WheelPayload> = {
  key: "spin-wheel",
  name: "Spin the Wheel",
  description: "Take turns spinning a wheel of challenges, prizes and forfeits. The server decides where it lands, so nobody can steer it.",
  hook: "Round and round it goes.",
  category: "reflex",
  accent: "tomato",
  Glyph: SpinWheelGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: { segments: [""], useBuiltIn: true, spins: 6 },
  isSetupValid: isWheelSetupValid,
  setupIssue: wheelSetupIssue,
  toApiSetup: toApiWheelSetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.SpinWheelSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.SpinWheelGameScreen }))),
};
