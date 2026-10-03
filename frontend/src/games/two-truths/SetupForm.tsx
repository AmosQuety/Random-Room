import type { SetupFormProps } from "../types";
import type { TwoTruthsSetup } from "./types";

/** Nothing to configure: the players supply all the content, one turn each. */
export function TwoTruthsSetupForm(_props: SetupFormProps<TwoTruthsSetup>) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-card p-4">
      <p className="font-bold">No setup needed.</p>
      <p className="text-sm text-muted">
        Everyone takes one turn as storyteller: write two true statements and one lie about yourself. The others vote on the lie.
        A correct guess scores 1, and the storyteller scores 1 for every player they fool.
      </p>
    </div>
  );
}
