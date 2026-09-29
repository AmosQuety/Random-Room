import { StarIcon } from "../../components/icons";

interface Props {
  roundNumber: number;
  tally: { choice: string; count: number }[];
}

export function FinalResult({ roundNumber, tally }: Props) {
  const total = tally.reduce((sum, t) => sum + t.count, 0);
  const top = Math.max(...tally.map((t) => t.count));
  const leaders = tally.filter((t) => t.count === top);
  const headline = total === 0 ? "No rolls this round" : leaders.length > 1 ? "It's a tie" : `${leaders[0].choice} takes it`;

  return (
    <section
      aria-labelledby="final-heading"
      className="surface-dark animate-stamp rounded-xl border-2 border-ink bg-ink p-6 text-center text-paper shadow-ticket"
    >
      <p id="final-heading" className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">
        Final result · round {roundNumber}
      </p>
      <p className="mt-3 flex items-center justify-center gap-3 font-display text-4xl font-black sm:text-5xl">
        <StarIcon className="size-7 shrink-0 text-mustard" />
        {headline}
        <StarIcon className="size-7 shrink-0 text-mustard" />
      </p>
      <dl className="mt-6 flex flex-wrap justify-center gap-x-10 gap-y-4">
        {tally.map((t) => (
          <div key={t.choice}>
            <dt className="text-lg font-semibold">{t.choice}</dt>
            <dd className="font-display text-6xl font-black text-mustard">{t.count}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-4 text-sm text-paper/80">
        Counted from {total} system random {total === 1 ? "result" : "results"}. These are locked and cannot be changed.
      </p>
    </section>
  );
}
