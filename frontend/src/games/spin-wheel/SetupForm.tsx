import { ListEditor } from "../../components/ListEditor";
import { fieldInputClass } from "../../components/styles";
import type { SetupFormProps } from "../types";
import { filledSegments } from "./setup";
import { BUILT_IN_COUNT, MAX_SEGMENTS, MIN_SEGMENTS, SPIN_CHOICES, type WheelSetup } from "./types";

export function SpinWheelSetupForm({ value, onChange }: SetupFormProps<WheelSetup>) {
  const filled = filledSegments(value.segments);
  const duplicated = new Set(filled.map((s) => s.toLowerCase())).size !== filled.length;
  const update = (patch: Partial<WheelSetup>) => onChange({ ...value, ...patch });

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
          <span className="block font-bold">Fill the wheel with built-in challenges</span>
          <span className="block text-sm text-muted">
            {BUILT_IN_COUNT} silly, family friendly ones. Your own always go on the wheel; built-in ones top it up to 8 segments.
          </span>
        </span>
      </label>

      <ListEditor
        legend="Your own segments"
        hint={`Up to ${MAX_SEGMENTS}. Each is a challenge, a prize or a forfeit. Leave empty to use only the built-in ones.`}
        items={value.segments}
        onChange={(segments) => update({ segments })}
        placeholder={(i) => `Segment ${i + 1}`}
        minItems={0}
        maxItems={MAX_SEGMENTS}
        maxLength={80}
        addLabel="Add a segment"
      />

      <div>
        <label htmlFor="spins" className="mb-2 block font-mono text-sm font-bold uppercase tracking-widest">
          Spins
        </label>
        <select id="spins" value={value.spins} onChange={(e) => update({ spins: Number(e.target.value) })} className={fieldInputClass}>
          {SPIN_CHOICES.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
        <p className="mt-1 text-sm text-muted">Players take turns, one spin each, in the order they joined.</p>
      </div>

      <p role="status" className="text-sm font-semibold text-tomato">
        {duplicated
          ? "Each segment needs to be different."
          : !value.useBuiltIn && filled.length < MIN_SEGMENTS
            ? `Add at least ${MIN_SEGMENTS} segments, or turn on the built-in challenges.`
            : ""}
      </p>
    </div>
  );
}
