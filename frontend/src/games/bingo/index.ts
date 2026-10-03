import { lazy } from "react";
import type { GameModule } from "../types";
import { BingoGlyph } from "./Glyph";
import { bingoSetupIssue, isBingoSetupValid, toApiBingoSetup } from "./setup";
import type { BingoPayload, BingoSetup } from "./types";

export const bingoModule: GameModule<BingoSetup, BingoPayload> = {
  key: "bingo",
  name: "Bingo",
  description: "Everyone gets their own card. The host calls items, you mark them, and the first valid bingo wins. The server checks every mark and claim.",
  hook: "Five in a row, no cheating.",
  category: "reflex",
  accent: "sky",
  Glyph: BingoGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: { items: [""], useBuiltIn: true },
  isSetupValid: isBingoSetupValid,
  setupIssue: bingoSetupIssue,
  toApiSetup: toApiBingoSetup,
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.BingoSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.BingoGameScreen }))),
};
