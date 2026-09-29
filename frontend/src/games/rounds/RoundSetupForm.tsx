import type { ComponentType } from "react";
import { PlusIcon } from "../../components/icons";
import { addButtonClass, fieldInputClass, removeButtonClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import {
  availablePromptCount,
  effectiveRounds,
  roundOptions,
  TIME_LIMIT_CHOICES,
  type PromptKit,
  type RoundSetupState,
} from "./setup";

export interface PromptEditorProps<TInput> {
  value: TInput;
  onChange: (value: TInput) => void;
  index: number;
  /** True when the row has been started but is not yet usable. */
  invalid: boolean;
}

interface Props<TInput> extends SetupFormProps<RoundSetupState<TInput>> {
  kit: PromptKit<TInput>;
  /** Singular and plural names for one prompt, e.g. "dilemma" / "dilemmas". */
  noun: [string, string];
  PromptEditor: ComponentType<PromptEditorProps<TInput>>;
}

const selectClass = fieldInputClass;

/** The setup form shared by every prompt-and-answer game. A game supplies how one prompt is edited. */
export function RoundSetupForm<TInput>({ value, onChange, kit, noun, PromptEditor }: Props<TInput>) {
  const [singular, plural] = noun;
  const total = availablePromptCount(value, kit);
  const rounds = effectiveRounds(value, kit);
  const update = (patch: Partial<RoundSetupState<TInput>>) => onChange({ ...value, ...patch });

  return (
    <div className="flex flex-col gap-6">
      {kit.builtInCount > 0 && (
        <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border-2 border-ink bg-card p-4">
          <input
            type="checkbox"
            checked={value.useBuiltIn}
            onChange={(e) => update({ useBuiltIn: e.target.checked })}
            className="mt-1 size-5 shrink-0 accent-[var(--accent)]"
          />
          <span>
            <span className="block font-bold">Use the built-in {plural}</span>
            <span className="block text-sm text-muted">{kit.builtInCount} ready-made, family friendly. Add your own below to mix them in.</span>
          </span>
        </label>
      )}

      <fieldset className="flex flex-col gap-3">
        <legend className="font-mono text-sm font-bold uppercase tracking-widest">Your own {plural}</legend>
        {value.custom.map((prompt, i) => (
          <div key={i} className="flex gap-2">
            <div className="min-w-0 flex-1">
              <PromptEditor
                value={prompt}
                index={i}
                invalid={kit.isFilled(prompt) && !kit.isValid(prompt)}
                onChange={(next) => update({ custom: value.custom.map((p, j) => (j === i ? next : p)) })}
              />
            </div>
            {(value.custom.length > 1 || kit.builtInCount > 0) && (
              <button
                type="button"
                aria-label={`Remove ${singular} ${i + 1}`}
                onClick={() => update({ custom: value.custom.filter((_, j) => j !== i) })}
                className={removeButtonClass}
              >
                ×
              </button>
            )}
          </div>
        ))}
        <button type="button" onClick={() => update({ custom: [...value.custom, kit.empty()] })} className={addButtonClass}>
          <PlusIcon className="size-4" /> Add a {singular}
        </button>
      </fieldset>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="rounds" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
            Rounds
          </label>
          <select id="rounds" value={rounds} disabled={total < 1} onChange={(e) => update({ rounds: Number(e.target.value) })} className={selectClass}>
            {roundOptions(total, rounds).map((n) => (
              <option key={n} value={n}>
                {n}
                {n === total ? " (all)" : ""}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="time-limit" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
            Time per round
          </label>
          <select
            id="time-limit"
            value={value.timeLimit ?? ""}
            onChange={(e) => update({ timeLimit: e.target.value === "" ? null : Number(e.target.value) })}
            className={selectClass}
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

      <p role="status" className="text-sm text-muted">
        {total === 0 ? `Add at least one ${singular}, or use the built-in ones.` : `${total} ${total === 1 ? singular : plural} available, playing ${rounds}.`}
      </p>
    </div>
  );
}
