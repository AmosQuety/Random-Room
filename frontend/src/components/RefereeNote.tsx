import type { ReactNode } from "react";

interface Props {
  /** What the referee can do on this screen, in a short sentence. */
  children?: ReactNode;
}

/** Shown to a host who referees: they run the game and do not play. One wording for every game. */
export function RefereeNote({ children }: Props) {
  return (
    <section aria-label="You are refereeing" className="rounded-lg border-2 border-ink bg-accent-soft px-4 py-3">
      <p className="font-mono text-xs font-bold uppercase tracking-widest">You are refereeing</p>
      <p className="mt-1 text-sm">You run the game and do not play. {children}</p>
    </section>
  );
}
