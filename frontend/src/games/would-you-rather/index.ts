import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { TwoWayInput, TwoWayPayload } from "../rounds/twoWay";
import type { GameModule } from "../types";
import { WouldYouRatherGlyph } from "./Glyph";
import { kit } from "./kit";

export const wouldYouRatherModule: GameModule<RoundSetupState<TwoWayInput>, TwoWayPayload> = {
  key: "would-you-rather",
  name: "Would You Rather",
  description: "Two dilemmas, one private pick each, then everyone's choices reveal together. Sit with the majority to score.",
  hook: "Impossible choices, revealed together.",
  category: "poll",
  accent: "plum",
  Glyph: WouldYouRatherGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.WouldYouRatherSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.WouldYouRatherGameScreen }))),
};
