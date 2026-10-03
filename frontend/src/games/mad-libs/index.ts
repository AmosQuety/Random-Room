import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { GameModule } from "../types";
import type { MadLibsPayload } from "./GameScreen";
import { MadLibsGlyph } from "./Glyph";
import { kit, type StoryInput } from "./kit";

export const madLibsModule: GameModule<RoundSetupState<StoryInput>, MadLibsPayload> = {
  key: "mad-libs",
  name: "Fill-in Stories",
  description: "Everyone fills in a few blanks of a story they cannot see, then the finished story is read out. 30 built-in stories, or write your own.",
  hook: "Blind blanks, ridiculous stories.",
  category: "story",
  accent: "plum",
  Glyph: MadLibsGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.MadLibsSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.MadLibsGameScreen }))),
};
