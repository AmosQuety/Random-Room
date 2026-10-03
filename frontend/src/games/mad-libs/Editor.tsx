import { fieldInputClass } from "../../components/styles";
import type { PromptEditorProps } from "../rounds/RoundSetupForm";
import { storyProblem, type StoryInput } from "./kit";

export function StoryEditor({ value, onChange, index, invalid }: PromptEditorProps<StoryInput>) {
  const problem = invalid ? storyProblem(value) : null;
  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper p-3">
      <input
        value={value.title}
        maxLength={60}
        aria-label={`Title of story ${index + 1}`}
        aria-invalid={invalid && value.title.trim() === ""}
        placeholder="Story title"
        onChange={(e) => onChange({ ...value, title: e.target.value })}
        className={fieldInputClass}
      />
      <textarea
        value={value.text}
        maxLength={600}
        rows={4}
        aria-label={`Text of story ${index + 1}`}
        aria-invalid={problem !== null}
        placeholder="The {adjective} {animal} went to the {place}."
        onChange={(e) => onChange({ ...value, text: e.target.value })}
        className={fieldInputClass}
      />
      {problem && <p className="text-sm font-semibold text-tomato">{problem}</p>}
    </div>
  );
}
