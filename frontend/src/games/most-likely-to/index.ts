import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { StatementInput } from "../rounds/statement";
import type { GameModule } from "../types";
import { MostLikelyToGlyph } from "./Glyph";
import { kit } from "./kit";
import type { MostLikelyToPayload } from "./GameScreen";

export const mostLikelyToModule: GameModule<RoundSetupState<StatementInput>, MostLikelyToPayload> = {
  key: "most-likely-to",
  name: "Most Likely To",
  description: "A prompt appears and everyone secretly votes for the friend it fits best. The most-voted player wears the crown.",
  hook: "Vote for the friend who fits. Crowns for the winners.",
  category: "poll",
  accent: "leaf",
  Glyph: MostLikelyToGlyph,
  minPlayers: 3,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.MostLikelyToSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.MostLikelyToGameScreen }))),
};
