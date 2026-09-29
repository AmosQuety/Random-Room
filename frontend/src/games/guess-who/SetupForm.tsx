import type { SetupFormProps } from "../types";
import type { GuessWhoSetup } from "./types";

/** Nothing to configure: the players write the content. */
export function GuessWhoSetupForm(_props: SetupFormProps<GuessWhoSetup>) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-card p-4">
      <p className="font-bold">No setup needed.</p>
      <p className="text-sm text-muted">
        Everyone writes one thing about themselves that others might not know. The facts come up one at a time and the group
        guesses who wrote each one. A correct guess scores 1. Nobody sees who wrote a fact until it is revealed.
      </p>
    </div>
  );
}
