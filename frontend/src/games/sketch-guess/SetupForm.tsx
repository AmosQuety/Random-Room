import { ListEditor } from "../../components/ListEditor";
import { fieldInputClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import { filledWords } from "./setup";
import { BUILT_IN_COUNT, MAX_CUSTOM_WORDS, MAX_WORD_LENGTH, ROUND_CHOICES, TIME_CHOICES, type SketchSetup } from "./types";

export function SketchGuessSetupForm({ value, onChange }: SetupFormProps<SketchSetup>) {
  const filled = filledWords(value.words);
  const duplicated = new Set(filled.map((w) => w.toLowerCase())).size !== filled.length;
  const update = (patch: Partial<SketchSetup>) => onChange({ ...value, ...patch });

  return (
    <div className="flex flex-col gap-6">
      <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border-2 border-ink bg-card p-4">
        <input type="checkbox" checked={value.useBuiltIn} onChange={(e) => update({ useBuiltIn: e.target.checked })} className="mt-1 size-5 shrink-0 accent-[var(--accent)]" />
        <span>
          <span className="block font-bold">Also draw from the built-in words</span>
          <span className="block text-sm text-muted">{BUILT_IN_COUNT} easy-to-draw, family friendly words. Your own words are always used first.</span>
        </span>
      </label>

      <ListEditor
        legend="Your own words"
        hint={`Up to ${MAX_CUSTOM_WORDS}. Things that can be drawn: a bicycle, a birthday cake. Leave empty to use only the built-in words.`}
        items={value.words}
        onChange={(words) => update({ words })}
        placeholder={(i) => `Word ${i + 1}`}
        minItems={0}
        maxItems={MAX_CUSTOM_WORDS}
        maxLength={MAX_WORD_LENGTH}
        addLabel="Add a word"
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="sketch-rounds" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
            Rounds
          </label>
          <select id="sketch-rounds" value={value.rounds} onChange={(e) => update({ rounds: Number(e.target.value) })} className={fieldInputClass}>
            {ROUND_CHOICES.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
          <p className="mt-1 text-sm text-muted">Players take turns drawing, in the order they joined.</p>
        </div>
        <div>
          <label htmlFor="sketch-time" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
            Time to draw
          </label>
          <select id="sketch-time" value={value.timeLimitSeconds} onChange={(e) => update({ timeLimitSeconds: Number(e.target.value) })} className={fieldInputClass}>
            {TIME_CHOICES.map((n) => (
              <option key={n} value={n}>
                {n} seconds
              </option>
            ))}
          </select>
        </div>
      </div>

      <p role="status" className="text-sm font-semibold text-tomato">
        {duplicated ? "Each word needs to be different." : !value.useBuiltIn && filled.length < 1 ? "Add at least one word, or turn on the built-in words." : ""}
      </p>
    </div>
  );
}
