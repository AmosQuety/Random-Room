import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { StatementInput } from "../rounds/statement";
import type { SetupFormProps } from "../types";
import { MostLikelyToEditor } from "./Editor";
import { kit } from "./kit";

export function MostLikelyToSetupForm(props: SetupFormProps<RoundSetupState<StatementInput>>) {
  return <RoundSetupForm {...props} kit={kit} noun={["prompt", "prompts"]} PromptEditor={MostLikelyToEditor} />;
}
