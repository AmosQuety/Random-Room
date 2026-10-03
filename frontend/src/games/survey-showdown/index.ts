import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { GameModule } from "../types";
import type { SurveyShowdownPayload } from "./GameScreen";
import { SurveyShowdownGlyph } from "./Glyph";
import { kit, type SurveyInput } from "./kit";

export const surveyShowdownModule: GameModule<RoundSetupState<SurveyInput>, SurveyShowdownPayload> = {
  key: "survey-showdown",
  name: "Survey Showdown",
  description: "A survey question with a hidden board of answers. Type your guess; matching a board answer scores its points.",
  hook: "Guess what most people said.",
  category: "poll",
  accent: "sky",
  Glyph: SurveyShowdownGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  ...roundSetupFor(kit),
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.SurveyShowdownSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.SurveyShowdownGameScreen }))),
};
