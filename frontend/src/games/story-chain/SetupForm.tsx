import { ListEditor } from "../../components/ListEditor";
import { fieldInputClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import { BUILT_IN_OPENERS, MAX_OPENERS, type ChainConfig, type ChainSetup } from "./types";

interface Props extends SetupFormProps<ChainSetup> {
  config: ChainConfig;
}

export function ChainSetupForm({ value, onChange, config }: Props) {
  const update = (patch: Partial<ChainSetup>) => onChange({ ...value, ...patch });
  return (
    <div className="flex flex-col gap-6">
      <p className="rounded-lg border-2 border-ink bg-card p-4 text-sm text-muted">{config.intro}</p>

      <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border-2 border-ink bg-card p-4">
        <input
          type="checkbox"
          checked={value.useBuiltIn}
          onChange={(e) => update({ useBuiltIn: e.target.checked })}
          className="mt-1 size-5 shrink-0 accent-[var(--accent)]"
        />
        <span>
          <span className="block font-bold">Use the built-in story starters</span>
          <span className="block text-sm text-muted">{BUILT_IN_OPENERS} family friendly openers. One is picked at random when the game starts.</span>
        </span>
      </label>

      <ListEditor
        legend="Your own starters"
        hint="Optional. One of all the starters is picked at random."
        items={value.openers}
        onChange={(openers) => update({ openers })}
        placeholder={(i) => `Starter ${i + 1}`}
        minItems={0}
        maxItems={MAX_OPENERS}
        maxLength={140}
        addLabel="Add a starter"
      />

      <div>
        <label htmlFor="length" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
          {config.lengthLabel}
        </label>
        <select id="length" value={value.length} onChange={(e) => update({ length: Number(e.target.value) })} className={fieldInputClass}>
          {config.lengthChoices.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
        <p className="mt-1 text-sm text-muted">Players take turns in the order they joined. The host can skip a turn or end the story early.</p>
      </div>
    </div>
  );
}
