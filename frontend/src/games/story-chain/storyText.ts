import type { ChainEntry } from "./types";

/** One turn as it reads in the story: the lead-in the server added (if the game has one), then what the player wrote. */
export const entryText = (entry: ChainEntry): string => (entry.prefix ? `${entry.prefix} ${entry.text}` : entry.text);
