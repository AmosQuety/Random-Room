import { lazy, createElement } from "react";
import type { GameModule, GameScreenProps, SetupFormProps } from "../types";
import { FortunatelyGlyph } from "../story-chain/glyphs";
import { defaultChainSetup, chainSetupIssue, isChainSetupValid, toApiChainSetup } from "../story-chain/setup";
import type { ChainConfig, ChainPayload, ChainSetup } from "../story-chain/types";

export const fortunatelyConfig: ChainConfig = {
  piece: "sentence",
  lengthLabel: "Turns in the story",
  lengthChoices: [4, 6, 8, 10, 12, 16, 20, 30],
  defaultLength: 12,
  maxChars: 140,
  singleWord: false,
  inputHint: "What happens next?",
  intro: "The story swings between good luck and bad. The game starts each turn for you with \"Fortunately,\" or \"Unfortunately,\" in turn, and you finish the sentence.",
};

export const fortunatelyModule: GameModule<ChainSetup, ChainPayload> = {
  key: "fortunately",
  name: "Fortunately / Unfortunately",
  description: "A story that swings between good luck and bad, one sentence per turn. The game supplies the lead-in.",
  hook: "Good news, bad news, repeat.",
  category: "story",
  accent: "leaf",
  Glyph: FortunatelyGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: defaultChainSetup(fortunatelyConfig),
  isSetupValid: isChainSetupValid,
  setupIssue: chainSetupIssue,
  toApiSetup: toApiChainSetup,
  SetupForm: lazy(() => import("../story-chain/SetupForm").then((m) => ({ default: (p: SetupFormProps<ChainSetup>) => createElement(m.ChainSetupForm, { ...p, config: fortunatelyConfig }) }))),
  GameScreen: lazy(() => import("../story-chain/GameScreen").then((m) => ({ default: (p: GameScreenProps<ChainPayload>) => createElement(m.ChainGameScreen, { ...p, config: fortunatelyConfig }) }))),
};
