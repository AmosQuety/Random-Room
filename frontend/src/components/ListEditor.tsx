export const fieldInputClass = "min-h-12 w-full rounded-lg border-2 border-ink bg-card px-3 text-lg";
export const removeButtonClass = "grid size-10 shrink-0 place-items-center rounded-lg border-2 border-ink font-black text-muted";
export const addButtonClass =
  "min-h-12 rounded-lg border-2 border-dashed border-ink px-4 font-mono text-sm uppercase tracking-widest text-muted transition hover:bg-card";

interface Props {
  legend: string;
  hint: string;
  items: string[];
  onChange: (items: string[]) => void;
  placeholder: (index: number) => string;
  minItems?: number;
}

/** A labelled, add/remove-able list of plain text inputs - choices, players, question options. */
export function ListEditor({ legend, hint, items, onChange, placeholder, minItems = 2 }: Props) {
  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="font-mono text-sm uppercase tracking-widest">{legend}</legend>
      <p className="-mt-1 text-sm text-muted">{hint}</p>
      {items.map((value, i) => (
        <div key={i} className="flex gap-2">
          <input
            value={value}
            placeholder={placeholder(i)}
            onChange={(e) => onChange(items.map((v, j) => (j === i ? e.target.value : v)))}
            className={fieldInputClass}
          />
          {items.length > minItems && (
            <button
              type="button"
              aria-label={`Remove ${legend.toLowerCase()} ${i + 1}`}
              onClick={() => onChange(items.filter((_, j) => j !== i))}
              className={removeButtonClass}
            >
              ×
            </button>
          )}
        </div>
      ))}
      <button type="button" onClick={() => onChange([...items, ""])} className={addButtonClass}>
        + Add another
      </button>
    </fieldset>
  );
}
