import { fieldInputClass } from "../../components/styles";
import type { PromptEditorProps } from "./RoundSetupForm";
import type { StatementInput, StatementPromptView } from "./statement";

export function StatementEditor({ value, onChange, index, invalid, placeholder }: PromptEditorProps<StatementInput> & { placeholder: string }) {
  return (
    <input
      value={value.text}
      maxLength={140}
      aria-label={`Prompt ${index + 1}`}
      aria-invalid={invalid}
      placeholder={placeholder}
      onChange={(e) => onChange({ text: e.target.value })}
      className={fieldInputClass}
    />
  );
}

export function StatementText({ prompt }: { prompt: StatementPromptView }) {
  return <h2 className="font-display text-2xl font-black leading-tight sm:text-3xl">{prompt.text}</h2>;
}
