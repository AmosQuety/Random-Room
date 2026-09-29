import { ListEditor } from "../../components/ListEditor";
import { addButtonClass, fieldInputClass, removeButtonClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import type { TriviaQuestionInput, TriviaSetup } from "./types";

function QuestionEditor({
  question,
  index,
  canRemove,
  onChange,
  onRemove,
}: {
  question: TriviaQuestionInput;
  index: number;
  canRemove: boolean;
  onChange: (question: TriviaQuestionInput) => void;
  onRemove: () => void;
}) {
  return (
    <div className="flex flex-col gap-3 rounded-lg border-2 border-ink bg-paper p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="font-mono text-xs uppercase tracking-widest text-muted">Question {index + 1}</p>
        {canRemove && (
          <button type="button" aria-label={`Remove question ${index + 1}`} onClick={onRemove} className={removeButtonClass}>
            ×
          </button>
        )}
      </div>

      <input
        value={question.text}
        onChange={(e) => onChange({ ...question, text: e.target.value })}
        placeholder="What's the question?"
        className={fieldInputClass}
      />

      <ListEditor
        legend="Options"
        hint="Pick the correct one below."
        items={question.options}
        onChange={(options) => onChange({ ...question, options, correctIndex: Math.min(question.correctIndex, options.length - 1) })}
        placeholder={(i) => `Option ${i + 1}`}
      />

      <div>
        <label htmlFor={`correct-${index}`} className="mb-2 block font-mono text-xs uppercase tracking-widest text-muted">
          Correct answer
        </label>
        <select
          id={`correct-${index}`}
          value={question.correctIndex}
          onChange={(e) => onChange({ ...question, correctIndex: Number(e.target.value) })}
          className={fieldInputClass}
        >
          {question.options.map((option, i) => (
            <option key={i} value={i}>
              {option.trim() || `Option ${i + 1}`}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}

export function TriviaSetupForm({ value, onChange }: SetupFormProps<TriviaSetup>) {
  return (
    <fieldset className="flex flex-col gap-4">
      <legend className="font-mono text-sm uppercase tracking-widest">Questions</legend>
      <p className="-mt-1 text-sm text-muted">Add each question, its options, and mark the correct one.</p>

      {value.map((question, i) => (
        <QuestionEditor
          key={i}
          index={i}
          question={question}
          canRemove={value.length > 1}
          onChange={(updated) => onChange(value.map((q, j) => (j === i ? updated : q)))}
          onRemove={() => onChange(value.filter((_, j) => j !== i))}
        />
      ))}

      <button
        type="button"
        onClick={() => onChange([...value, { text: "", options: ["", ""], correctIndex: 0 }])}
        className={addButtonClass}
      >
        + Add another question
      </button>
    </fieldset>
  );
}
