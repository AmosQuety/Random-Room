import type { ReactNode } from "react";
import { CheckIcon } from "../../components/icons";

interface Props {
  label: ReactNode;
  count: number;
  total: number;
  /** What one count is called, e.g. "vote". Pluralised with an s. */
  unit: string;
  /** Marks the winning row with a check and the word, never colour alone. */
  highlight?: string;
  people?: string[];
  mine?: boolean;
  /**
   * "share": the bar and the percentage are a share of everyone (polls).
   * "relative": the bar is only relative to the best answer, so no percentage is shown (Survey Showdown points).
   */
  measure?: "share" | "relative";
}

export function ResultBar({ label, count, total, unit, highlight, people = [], mine = false, measure = "share" }: Props) {
  const percent = total > 0 ? Math.round((count / total) * 100) : 0;
  return (
    <li className={`rounded-lg border-2 border-ink bg-card p-3 ${mine ? "ring-4 ring-mustard/60" : ""}`}>
      <div className="flex items-start justify-between gap-3">
        <p className="min-w-0 flex-1 font-display text-lg font-bold leading-snug">{label}</p>
        <p className="shrink-0 font-mono text-sm font-bold">
          {count} {unit}
          {count === 1 ? "" : "s"}
          {measure === "share" && ` · ${percent}%`}
        </p>
      </div>
      <div aria-hidden="true" className="mt-2 h-3 overflow-hidden rounded-full border-2 border-ink bg-paper">
        <div className="h-full bg-accent motion-safe:transition-[width] motion-safe:duration-500" style={{ width: `${percent}%` }} />
      </div>
      {(highlight || people.length > 0 || mine) && (
        <p className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          {highlight && (
            <strong className="inline-flex items-center gap-1 text-accent-ink">
              <CheckIcon className="size-4" /> {highlight}
            </strong>
          )}
          {mine && <span className="font-semibold">Your pick</span>}
          {people.length > 0 && <span className="text-muted">{people.join(", ")}</span>}
        </p>
      )}
    </li>
  );
}
