import { ListEditor } from "../../components/ListEditor";
import { addButtonClass, fieldInputClass, removeButtonClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import {
  emptyQuestion,
  MAX_CATEGORY_LENGTH,
  MAX_OPTION_LENGTH,
  MAX_OPTIONS,
  MAX_QUESTION_LENGTH,
  MAX_QUESTIONS,
  questionCount,
  STARTER_COUNT,
  STARTER_COUNT_CHOICES,
  TIME_LIMIT_CHOICES,
} from "./setup";
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
        maxLength={MAX_QUESTION_LENGTH}
        onChange={(e) => onChange({ ...question, text: e.target.value })}
        placeholder="What's the question?"
        aria-label={`Question ${index + 1} text`}
        className={fieldInputClass}
      />
      <input
        value={question.category}
        maxLength={MAX_CATEGORY_LENGTH}
        onChange={(e) => onChange({ ...question, category: e.target.value })}
        placeholder="Category (optional), e.g. Science"
        aria-label={`Question ${index + 1} category`}
        className={fieldInputClass}
      />

      <ListEditor
        legend="Options"
        hint="Pick the correct one below."
        items={question.options}
        onChange={(options) => onChange({ ...question, options, correctIndex: Math.min(question.correctIndex, options.length - 1) })}
        placeholder={(i) => `Option ${i + 1}`}
        maxItems={MAX_OPTIONS}
        maxLength={MAX_OPTION_LENGTH}
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
  const update = (patch: Partial<TriviaSetup>) => onChange({ ...value, ...patch });
  const setQuestions = (questions: TriviaQuestionInput[]) => update({ questions });

  return (
    <div className="flex flex-col gap-6">
      <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border-2 border-ink bg-card p-4">
        <input
          type="checkbox"
          checked={value.useBuiltIn}
          onChange={(e) => update({ useBuiltIn: e.target.checked })}
          className="mt-1 size-5 shrink-0 accent-[var(--accent)]"
        />
        <span>
          <span className="block font-bold">Add questions from the starter set</span>
          <span className="block text-sm text-muted">{STARTER_COUNT} general-knowledge questions across science, geography, history, nature and food. Yours come first.</span>
        </span>
      </label>

      {value.useBuiltIn && (
        <div>
          <label htmlFor="starter-count" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
            Starter questions
          </label>
          <select
            id="starter-count"
            value={value.builtInCount}
            onChange={(e) => update({ builtInCount: Number(e.target.value) })}
            className={fieldInputClass}
          >
            {STARTER_COUNT_CHOICES.map((n) => (
              <option key={n} value={n}>
                {n}
                {n === STARTER_COUNT ? " (all)" : ""}
              </option>
            ))}
          </select>
        </div>
      )}

      <fieldset className="flex flex-col gap-4">
        <legend className="font-mono text-sm font-bold uppercase tracking-widest">Your own questions</legend>
        <p className="-mt-1 text-sm text-muted">Add each question, its options, and mark the correct one. Blank rows are ignored.</p>

        {value.questions.map((question, i) => (
          <QuestionEditor
            key={i}
            index={i}
            question={question}
            canRemove={value.questions.length > 1 || value.useBuiltIn}
            onChange={(updated) => setQuestions(value.questions.map((q, j) => (j === i ? updated : q)))}
            onRemove={() => setQuestions(value.questions.filter((_, j) => j !== i))}
          />
        ))}

        <button
          type="button"
          disabled={questionCount(value) >= MAX_QUESTIONS}
          onClick={() => setQuestions([...value.questions, emptyQuestion()])}
          className={addButtonClass}
        >
          + Add another question
        </button>
        <p className="-mt-2 text-sm text-muted">
          {questionCount(value)} of {MAX_QUESTIONS} questions, yours and the starter ones together.
        </p>
      </fieldset>

      <div>
        <label htmlFor="trivia-time-limit" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
          Time per question
        </label>
        <select
          id="trivia-time-limit"
          value={value.timeLimit ?? ""}
          onChange={(e) => update({ timeLimit: e.target.value === "" ? null : Number(e.target.value) })}
          className={fieldInputClass}
        >
          <option value="">No limit</option>
          {TIME_LIMIT_CHOICES.map((s) => (
            <option key={s} value={s}>
              {s} seconds
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}
