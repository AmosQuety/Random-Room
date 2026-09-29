import { rankRows, type ScoreRow } from "../lib/ranking";
import { StarIcon } from "./icons";
import { Eyebrow } from "./ui";

interface Props {
  rows: ScoreRow[];
  me: string;
  /** What one point is called, e.g. "correct". Read aloud after the number. */
  unit?: string;
  title?: string;
}

/** The shared scoreboard: every scoring game renders its standings through this. */
export function Scoreboard({ rows, me, unit = "points", title = "Scoreboard" }: Props) {
  const ranked = rankRows(rows);
  const topScore = ranked[0]?.row.score ?? 0;

  return (
    <section aria-labelledby="scoreboard-heading">
      <h2 id="scoreboard-heading" className="mb-3 font-mono text-sm font-bold uppercase tracking-widest">
        {title}
      </h2>
      {ranked.length === 0 ? (
        <p className="text-muted">Nobody has scored yet.</p>
      ) : (
        <ol className="flex flex-col gap-2">
          {ranked.map(({ row, rank }) => {
            const leading = row.score === topScore && topScore > 0;
            return (
              <li
                key={row.name}
                className={`flex items-center gap-3 rounded-lg border-2 border-ink bg-card px-3 py-2 shadow-ticket-sm ${row.name === me ? "ring-4 ring-mustard/60" : ""}`}
              >
                <span
                  className={`grid size-9 shrink-0 place-items-center rounded-full border-2 border-ink font-mono text-sm font-bold ${leading ? "bg-mustard text-ink" : "bg-paper text-ink"}`}
                >
                  <span className="sr-only">Rank </span>
                  {rank}
                </span>
                <span className="min-w-0 flex-1 truncate font-display text-lg font-bold">
                  {row.name}
                  {row.name === me && <Eyebrow className="ml-2 inline">(you)</Eyebrow>}
                </span>
                {leading && <StarIcon className="size-5 text-mustard-ink" aria-hidden={false} aria-label="Leading" role="img" />}
                {/* key={score} replays the pop whenever the number changes. */}
                <span key={row.score} className="animate-pop font-display text-2xl font-black text-accent-ink">
                  {row.score}
                  <span className="sr-only"> {unit}</span>
                </span>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
