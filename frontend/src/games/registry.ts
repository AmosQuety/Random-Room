import { randomPickerModule } from "./random-picker";
import { triviaModule } from "./trivia";
import type { GameModule } from "./types";

/** A heterogeneous plugin registry - each module's own TSetup/TPayload types are only known inside its own folder. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const GAMES: Record<string, GameModule<any, any>> = {
  [randomPickerModule.key]: randomPickerModule,
  [triviaModule.key]: triviaModule,
};

export const GAME_LIST = Object.values(GAMES);
