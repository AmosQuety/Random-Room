import { CloseIcon, PlusIcon } from "./icons";
import { addButtonClass, fieldInputClass, removeButtonClass, sectionHeadingClass } from "./styles";

interface Props {
  legend: string;
  hint: string;
  items: string[];
  onChange: (items: string[]) => void;
  placeholder: (index: number) => string;
  minItems?: number;
  maxItems?: number;
  maxLength?: number;
  addLabel?: string;
}

/** A labelled, add/remove-able list of plain text inputs - choices, players, question options. */
export function ListEditor({
  legend,
  hint,
  items,
  onChange,
  placeholder,
  minItems = 2,
  maxItems = 50,
  maxLength = 200,
  addLabel = "Add another",
}: Props) {
  return (
    <fieldset className="flex min-w-0 flex-col gap-3">
      <legend className={sectionHeadingClass}>{legend}</legend>
      <p className="-mt-1 text-sm text-muted">{hint}</p>
      {items.map((value, i) => (
        <div key={i} className="flex gap-2">
          <input
            value={value}
            aria-label={placeholder(i)}
            placeholder={placeholder(i)}
            maxLength={maxLength}
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
              <CloseIcon />
            </button>
          )}
        </div>
      ))}
      <button type="button" disabled={items.length >= maxItems} onClick={() => onChange([...items, ""])} className={addButtonClass}>
        <PlusIcon className="size-4" />
        {addLabel}
      </button>
    </fieldset>
  );
}
