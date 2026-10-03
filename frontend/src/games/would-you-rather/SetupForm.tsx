import type { SetupFormProps } from "../types";
import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import { TwoWayEditor } from "../rounds/TwoWay";
import type { TwoWayInput } from "../rounds/twoWay";
import { kit } from "./kit";

export function WouldYouRatherSetupForm(props: SetupFormProps<RoundSetupState<TwoWayInput>>) {
  return <RoundSetupForm {...props} kit={kit} noun={["dilemma", "dilemmas"]} PromptEditor={TwoWayEditor} />;
}
