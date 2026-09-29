import { lazy } from "react";
import { roundSetupFor, type RoundSetupState } from "../rounds/setup";
import type { GameModule } from "../types";
import { ForbiddenGlyph } from "./Glyph";
import { kit, type CardInput } from "./kit";
import type { ForbiddenPayload } from "./types";

const base = roundSetupFor(kit);

export const forbiddenWordsModule: GameModule<RoundSetupState<CardInput>, ForbiddenPayload> = {
  key: "forbidden-words",
  name: "Forbidden Words",
  description: "Describe the secret word without saying the forbidden ones while everyone else guesses. A judge who can see the card calls out slips.",
  hook: "Say it without saying it.",
  category: "word",
  accent: "tomato",
  Glyph: ForbiddenGlyph,
  minPlayers: 3,
  maxPlayers: 12,
  ...base,
  // The server calls the host's prompts "cards" for this game.
  toApiSetup: (setup) => {
    const { prompts, ...rest } = base.toApiSetup(setup) as { prompts: unknown[] } & Record<string, unknown>;
    return { ...rest, cards: prompts };
  },
  SetupForm: lazy(() => import("./SetupForm").then((m) => ({ default: m.ForbiddenSetupForm }))),
  GameScreen: lazy(() => import("./GameScreen").then((m) => ({ default: m.ForbiddenGameScreen }))),
};
