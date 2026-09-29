import { randomPickerModule } from "./random-picker";
import { triviaModule } from "./trivia";
import type { AnyGameModule } from "./types";

export const GAMES: Record<string, AnyGameModule> = {
  [randomPickerModule.key]: randomPickerModule,
  [triviaModule.key]: triviaModule,
};

export const GAME_LIST: AnyGameModule[] = Object.values(GAMES);
