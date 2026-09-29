import { mostLikelyToModule } from "./most-likely-to";
import { neverHaveIEverModule } from "./never-have-i-ever";
import { randomPickerModule } from "./random-picker";
import { surveyShowdownModule } from "./survey-showdown";
import { thisOrThatModule } from "./this-or-that";
import { triviaModule } from "./trivia";
import { twoTruthsModule } from "./two-truths";
import type { AnyGameModule } from "./types";
import { wouldYouRatherModule } from "./would-you-rather";

export const GAMES: Record<string, AnyGameModule> = {
  [randomPickerModule.key]: randomPickerModule,
  [triviaModule.key]: triviaModule,
  [wouldYouRatherModule.key]: wouldYouRatherModule,
  [thisOrThatModule.key]: thisOrThatModule,
  [mostLikelyToModule.key]: mostLikelyToModule,
  [neverHaveIEverModule.key]: neverHaveIEverModule,
  [surveyShowdownModule.key]: surveyShowdownModule,
  [twoTruthsModule.key]: twoTruthsModule,
};

export const GAME_LIST: AnyGameModule[] = Object.values(GAMES);
