import { fieldInputClass } from "../../components/styles";
import type { PromptEditorProps } from "../rounds/RoundSetupForm";
import { cardProblem, MAX_WORD, type CardInput } from "./kit";

export function CardEditor({ value, onChange, index, invalid }: PromptEditorProps<CardInput>) {
  const problem = invalid ? cardProblem(value) : null;
  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper p-3">
      <input
        value={value.word}
        maxLength={MAX_WORD}
        aria-label={`Secret word for card ${index + 1}`}
        aria-invalid={invalid && value.word.trim() === ""}
        placeholder="The secret word"
        onChange={(e) => onChange({ ...value, word: e.target.value })}
        className={fieldInputClass}
      />
      <input
        value={value.forbidden}
        maxLength={200}
        aria-label={`Forbidden words for card ${index + 1}`}
        aria-invalid={problem !== null}
        placeholder="Forbidden words, separated by commas"
        onChange={(e) => onChange({ ...value, forbidden: e.target.value })}
        className={fieldInputClass}
      />
      {problem && <p className="text-sm font-semibold text-tomato">{problem}</p>}
    </div>
  );
}
