import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { SetupFormProps } from "../types";
import { CardEditor } from "./Editor";
import { kit, type CardInput } from "./kit";

export function ForbiddenSetupForm(props: SetupFormProps<RoundSetupState<CardInput>>) {
  return (
    <div className="flex flex-col gap-4">
      <p className="rounded-lg border-2 border-ink bg-card p-4 text-sm text-muted">
        One player describes the secret word without saying the forbidden ones, and everyone else types guesses. The next player
        also sees the card and calls out slips. Needs 3 or more players. A correct guess scores 1 for the guesser and the
        describer; a slip costs the describer a point.
      </p>
      <RoundSetupForm {...props} kit={kit} noun={["card", "cards"]} PromptEditor={CardEditor} />
    </div>
  );
}
