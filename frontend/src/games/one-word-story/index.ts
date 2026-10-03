import { lazy, createElement } from "react";
import type { GameModule, GameScreenProps, SetupFormProps } from "../types";
import { OneWordGlyph } from "../story-chain/glyphs";
import { defaultChainSetup, chainSetupIssue, isChainSetupValid, toApiChainSetup } from "../story-chain/setup";
import type { ChainConfig, ChainPayload, ChainSetup } from "../story-chain/types";

export const oneWordConfig: ChainConfig = {
  piece: "word",
  lengthLabel: "Words in the story",
  lengthChoices: [10, 20, 30, 40, 60, 80, 100],
  defaultLength: 40,
  maxChars: 24,
  singleWord: true,
  inputHint: "Add one word",
  intro: "The group builds a story one word at a time. You only get to add a single word on your turn, so make it count. The story starts from an opener and ends after the set number of words.",
};

export const oneWordStoryModule: GameModule<ChainSetup, ChainPayload> = {
  key: "one-word-story",
  name: "One-Word Story",
  description: "Build a story together, one word per turn, from a random opener. Nobody controls where it goes.",
  hook: "One word each. Where does it end up?",
  category: "story",
  accent: "plum",
  Glyph: OneWordGlyph,
  minPlayers: 2,
  maxPlayers: 12,
  defaultSetup: defaultChainSetup(oneWordConfig),
  isSetupValid: isChainSetupValid,
  setupIssue: chainSetupIssue,
  toApiSetup: toApiChainSetup,
  SetupForm: lazy(() => import("../story-chain/SetupForm").then((m) => ({ default: (p: SetupFormProps<ChainSetup>) => createElement(m.ChainSetupForm, { ...p, config: oneWordConfig }) }))),
  GameScreen: lazy(() => import("../story-chain/GameScreen").then((m) => ({ default: (p: GameScreenProps<ChainPayload>) => createElement(m.ChainGameScreen, { ...p, config: oneWordConfig }) }))),
};
