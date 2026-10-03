import type { GameScreenProps } from "../types";
import { TwoWayGameScreen } from "../rounds/TwoWay";
import type { TwoWayPayload } from "../rounds/twoWay";

export function WouldYouRatherGameScreen(props: GameScreenProps<TwoWayPayload>) {
  return <TwoWayGameScreen {...props} lead="Would you rather..." />;
}
