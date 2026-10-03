import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { StatementInput } from "../rounds/statement";
import type { SetupFormProps } from "../types";
import { NeverHaveIEverEditor } from "./Editor";
import { kit } from "./kit";

export function NeverHaveIEverSetupForm(props: SetupFormProps<RoundSetupState<StatementInput>>) {
  return <RoundSetupForm {...props} kit={kit} noun={["statement", "statements"]} PromptEditor={NeverHaveIEverEditor} />;
}
