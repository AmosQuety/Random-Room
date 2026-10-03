import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { GameModule } from "../types";
import type { NameThatPayload } from "./GameScreen";
import { NameThatGlyph } from "./Glyph";
import { kit, type IntroInput } from "./kit";

export const nameThatModule: GameModule<RoundSetupState<IntroInput>, NameThatPayload> = {
  key: "name-that",
  name: "Name That Song or Movie",
  description: "The host writes clues (emojis, riddles, descriptions, optional clip links). Players type a guess; the server marks it.",
  hook: "Emoji riddles and intro clips. First correct guess earns a bonus.",
  category: "quiz",
  accent: "mustard",
  Glyph: NameThatGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.NameThatSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.NameThatGameScreen }))),
};
