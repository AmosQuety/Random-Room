import type { PromptEditorProps } from "../rounds/RoundSetupForm";
import { StatementEditor } from "../rounds/Statement";
import type { StatementInput } from "../rounds/statement";

export function MostLikelyToEditor(props: PromptEditorProps<StatementInput>) {
  return <StatementEditor {...props} placeholder="Most likely to..." />;
}
