import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { SetupFormProps } from "../types";
import { IntroEditor } from "./Editor";
import { kit, type IntroInput } from "./kit";

export function NameThatSetupForm(props: SetupFormProps<RoundSetupState<IntroInput>>) {
  return (
    <div className="flex flex-col gap-4">
      <p className="rounded-lg border-2 border-ink bg-card p-4 text-sm text-muted">
        Write each clue yourself: emojis, a riddle, or a description of a song or movie. A link to a clip is optional and opens
        in a new tab; pick one that does not show the title. Add every spelling you will accept - guesses are matched ignoring
        capital letters and punctuation.
      </p>
      <RoundSetupForm {...props} kit={kit} noun={["clue", "clues"]} PromptEditor={IntroEditor} />
    </div>
  );
}
