import { ListEditor } from "../../components/ListEditor";
import { fieldInputClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import { filledPrompts } from "./setup";
import { BUILT_IN_COUNT, MAX_PROMPTS, ROUND_CHOICES, type BuzzerSetup } from "./types";

export function BuzzerSetupForm({ value, onChange }: SetupFormProps<BuzzerSetup>) {
  const update = (patch: Partial<BuzzerSetup>) => onChange({ ...value, ...patch });
  const own = filledPrompts(value.prompts).length;
  const available = own + (value.useBuiltIn ? BUILT_IN_COUNT : 0);

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
          <span className="block font-bold">Use the built-in quick-fire prompts</span>
          <span className="block text-sm text-muted">{BUILT_IN_COUNT} family friendly prompts like "Name a fruit that is red". Mixed in with yours.</span>
        </span>
      </label>

      <ListEditor
        legend="Your own prompts"
        hint="The host reads each one out. The first player to buzz answers out loud and the host judges."
        items={value.prompts}
        onChange={(prompts) => update({ prompts })}
        placeholder={(i) => `Prompt ${i + 1}`}
        minItems={0}
        maxItems={MAX_PROMPTS}
        maxLength={140}
        addLabel="Add a prompt"
      />

      <div>
        <label htmlFor="rounds" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
          Rounds
        </label>
        <select id="rounds" value={value.rounds} onChange={(e) => update({ rounds: Number(e.target.value) })} className={fieldInputClass}>
          {ROUND_CHOICES.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
        <p className="mt-1 text-sm text-muted">
          {available === 0 ? "Add a prompt or use the built-in ones." : `${available} prompts available. The host judges, so the host does not buzz: this needs 3 or more players.`}
        </p>
      </div>
    </div>
  );
}
