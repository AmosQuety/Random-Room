import { bingoModule } from "./bingo";
import { buzzerModule } from "./buzzer";
import { guessWhoModule } from "./guess-who";
import { madLibsModule } from "./mad-libs";
import { mostLikelyToModule } from "./most-likely-to";
import { nameThatModule } from "./name-that";
import { neverHaveIEverModule } from "./never-have-i-ever";
import { randomPickerModule } from "./random-picker";
import { spinWheelModule } from "./spin-wheel";
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
  [guessWhoModule.key]: guessWhoModule,
  [nameThatModule.key]: nameThatModule,
  [spinWheelModule.key]: spinWheelModule,
  [buzzerModule.key]: buzzerModule,
  [bingoModule.key]: bingoModule,
  [madLibsModule.key]: madLibsModule,
};

export const GAME_LIST: AnyGameModule[] = Object.values(GAMES);
