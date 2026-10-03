import { ListEditor } from "../../components/ListEditor";
import type { SetupFormProps } from "../types";
import { filledItems, poolSize } from "./setup";
import { BUILT_IN_COUNT, MAX_ITEMS, MIN_POOL, type BingoSetup } from "./types";

export function BingoSetupForm({ value, onChange }: SetupFormProps<BingoSetup>) {
  const update = (patch: Partial<BingoSetup>) => onChange({ ...value, ...patch });
  const filled = filledItems(value.items);
  const duplicated = new Set(filled.map((i) => i.toLowerCase())).size !== filled.length;
  const total = poolSize(value);

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
          <span className="block font-bold">Use the built-in words</span>
          <span className="block text-sm text-muted">{BUILT_IN_COUNT} everyday words and things. Yours are mixed in.</span>
        </span>
      </label>

      <ListEditor
        legend="Your own items"
        hint={`Up to ${MAX_ITEMS}. Each player gets a differently arranged card of 24 of them. The host calls items one at a time.`}
        items={value.items}
        onChange={(items) => update({ items })}
        placeholder={(i) => `Item ${i + 1}`}
        minItems={0}
        maxItems={MAX_ITEMS}
        maxLength={40}
        addLabel="Add an item"
      />

      <p role="status" className={`text-sm ${duplicated || total < MIN_POOL ? "font-semibold text-tomato" : "text-muted"}`}>
        {duplicated
          ? "Each item needs to be different."
          : total < MIN_POOL
            ? `Bingo needs at least ${MIN_POOL} items. You have ${total}.`
            : `${total} items in the pool.`}
      </p>
    </div>
  );
}
