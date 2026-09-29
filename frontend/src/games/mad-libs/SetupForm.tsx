import { RoundSetupForm } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { SetupFormProps } from "../types";
import { StoryEditor } from "./Editor";
import { kit, MAX_BLANKS, MIN_BLANKS, type StoryInput } from "./kit";

export function MadLibsSetupForm(props: SetupFormProps<RoundSetupState<StoryInput>>) {
  return (
    <div className="flex flex-col gap-4">
      <p className="rounded-lg border-2 border-ink bg-card p-4 text-sm text-muted">
        Write a short story and mark each gap with the kind of word it needs, like {"{noun}"} or {"{silly word}"} ({MIN_BLANKS} to{" "}
        {MAX_BLANKS} gaps). Players fill in the gaps without seeing the story, then the finished story is revealed. Everyone who
        fills in their words scores a point.
      </p>
      <RoundSetupForm {...props} kit={kit} noun={["story", "stories"]} PromptEditor={StoryEditor} />
    </div>
  );
}
