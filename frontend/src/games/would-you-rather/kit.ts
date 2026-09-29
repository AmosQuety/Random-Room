import { twoWayKit } from "../rounds/twoWay";

/** Matches the number of built-in prompts the server ships (backend Games/Content/would-you-rather.json). */
export const BUILT_IN_COUNT = 30;
export const kit = twoWayKit(BUILT_IN_COUNT);
