import { fieldInputClass } from "../../components/styles";
import { RoundSetupForm, type PromptEditorProps } from "../rounds/RoundSetupForm";
import type { RoundSetupState } from "../rounds/setup";
import type { SetupFormProps } from "../types";
import { emptyRow, kit, MAX_BOARD, type SurveyInput } from "./kit";

function SurveyEditor({ value, onChange, index, invalid }: PromptEditorProps<SurveyInput>) {
  const setRow = (i: number, patch: Partial<SurveyInput["answers"][number]>) =>
    onChange({ ...value, answers: value.answers.map((a, j) => (j === i ? { ...a, ...patch } : a)) });

  return (
    <div className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper p-3">
      <input
        value={value.text}
        maxLength={140}
        aria-label={`Survey question ${index + 1}`}
        aria-invalid={invalid && value.text.trim() === ""}
        placeholder="Name something you find in a kitchen"
        onChange={(e) => onChange({ ...value, text: e.target.value })}
        className={fieldInputClass}
      />
      {value.answers.map((row, i) => (
        <div key={i} className="flex gap-2">
          <input
            value={row.text}
            maxLength={60}
            aria-label={`Board answer ${i + 1} for question ${index + 1}`}
            placeholder={`Answer ${i + 1}`}
            onChange={(e) => setRow(i, { text: e.target.value })}
            className={fieldInputClass}
          />
          <input
            value={row.points}
            inputMode="numeric"
            maxLength={3}
            aria-label={`Points for answer ${i + 1} of question ${index + 1}`}
            placeholder="Pts"
            onChange={(e) => setRow(i, { points: e.target.value.replace(/\D/g, "") })}
            className={`${fieldInputClass} !w-24 shrink-0`}
          />
        </div>
      ))}
      {value.answers.length < MAX_BOARD && (
        <button
          type="button"
          onClick={() => onChange({ ...value, answers: [...value.answers, emptyRow()] })}
          className="min-h-11 self-start rounded-lg px-2 font-mono text-sm font-bold uppercase tracking-widest text-muted underline"
        >
          + Add an answer
        </button>
      )}
      {invalid && <p className="text-sm font-semibold text-tomato">Needs a question and at least two answers with points from 1 to 100.</p>}
    </div>
  );
}

export function SurveyShowdownSetupForm(props: SetupFormProps<RoundSetupState<SurveyInput>>) {
  return <RoundSetupForm {...props} kit={kit} noun={["survey", "surveys"]} PromptEditor={SurveyEditor} />;
}
