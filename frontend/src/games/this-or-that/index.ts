import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { TwoWayInput, TwoWayPayload } from "../rounds/twoWay";
import type { GameModule } from "../types";
import { ThisOrThatGlyph } from "./Glyph";
import { kit } from "./kit";

export const thisOrThatModule: GameModule<RoundSetupState<TwoWayInput>, TwoWayPayload> = {
  key: "this-or-that",
  name: "This or That",
  description: "Rapid-fire pairs. Pick your side, see who agrees. Sit with the majority to score.",
  hook: "Two things. One tap. No overthinking.",
  category: "poll",
  accent: "mustard",
  Glyph: ThisOrThatGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.ThisOrThatSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.ThisOrThatGameScreen }))),
};
