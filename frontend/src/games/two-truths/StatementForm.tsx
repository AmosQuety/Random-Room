import { useState } from "react";
import { fieldInputClass } from "../../components/styles";
import { Button } from "../../components/ui";

interface Props {
  disabled: boolean;
  onSubmit: (body: { statements: string[]; lie: number }) => void;
}

/** The storyteller's form: three statements and which one is the lie. Nothing leaves this device until they submit. */
export function StatementForm({ disabled, onSubmit }: Props) {
  const [statements, setStatements] = useState(["", "", ""]);
  const [lie, setLie] = useState<number | null>(null);
  const trimmed = statements.map((s) => s.trim());
  const distinct = new Set(trimmed.map((s) => s.toLowerCase())).size === trimmed.length;
  const ready = trimmed.every(Boolean) && distinct && lie !== null;

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && lie !== null && !disabled) onSubmit({ statements: trimmed, lie });
      }}
      className="flex flex-col gap-4"
    >
      <fieldset className="flex flex-col gap-3">
        <legend className="mb-1 font-mono text-sm font-bold uppercase tracking-widest">Two truths and a lie about you</legend>
        {statements.map((value, i) => (
          <div key={i} className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper p-3">
            <label htmlFor={`statement-${i}`} className="font-semibold">
              Statement {i + 1}
            </label>
            <input
              id={`statement-${i}`}
              value={value}
              maxLength={140}
              autoComplete="off"
              onChange={(e) => setStatements(statements.map((s, j) => (j === i ? e.target.value : s)))}
              className={fieldInputClass}
            />
            <label className="flex min-h-11 cursor-pointer items-center gap-3">
              <input type="radio" name="lie" checked={lie === i} onChange={() => setLie(i)} className="size-5 accent-[var(--accent)]" />
              <span className="font-semibold">This one is the lie</span>
            </label>
          </div>
        ))}
      </fieldset>
      {!distinct && trimmed.every(Boolean) && <p className="text-sm font-semibold text-tomato">Each statement must be different.</p>}
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Lock them in
      </Button>
    </form>
  );
}
