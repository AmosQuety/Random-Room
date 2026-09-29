import type { GameScreenProps } from "../types";
import { TwoWayGameScreen } from "../rounds/TwoWay";
import type { TwoWayPayload } from "../rounds/twoWay";

export function ThisOrThatGameScreen(props: GameScreenProps<TwoWayPayload>) {
  return <TwoWayGameScreen {...props} lead="This or that?" />;
}
