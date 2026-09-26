import type { RoomSnapshot } from "../lib/types";

export function FinalResult({ snapshot }: { snapshot: RoomSnapshot }) {
  const { tally, round } = snapshot;
  const total = tally.reduce((sum, t) => sum + t.count, 0);
  const top = Math.max(...tally.map((t) => t.count));
  const leaders = tally.filter((t) => t.count === top);
  const headline = total === 0 ? "No rolls this round" : leaders.length > 1 ? "It's a tie" : `${leaders[0].choice} takes it`;

  return (
    <section aria-labelledby="final-heading" className="animate-stamp rounded-xl border-4 border-ink bg-ink p-6 text-center text-paper shadow-ticket">
      <p id="final-heading" className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">
        Final result · round {round.number}
      </p>
      <p className="mt-2 font-display text-4xl font-black sm:text-5xl">🎉 {headline} 🎉</p>
      <dl className="mt-6 flex justify-center gap-10">
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
