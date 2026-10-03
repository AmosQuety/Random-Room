import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { StatementInput } from "../rounds/statement";
import type { GameModule } from "../types";
import { NeverHaveIEverGlyph } from "./Glyph";
import { kit } from "./kit";
import type { NeverHaveIEverPayload } from "./GameScreen";

export const neverHaveIEverModule: GameModule<RoundSetupState<StatementInput>, NeverHaveIEverPayload> = {
  key: "never-have-i-ever",
  name: "Never Have I Ever",
  description: "Gentle 'never have I ever' statements. Everyone answers privately, then the confessions reveal. Never doing it keeps you standing.",
  hook: "Confess together. Keep your fingers up.",
  category: "poll",
  accent: "tomato",
  Glyph: NeverHaveIEverGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.NeverHaveIEverSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.NeverHaveIEverGameScreen }))),
};
