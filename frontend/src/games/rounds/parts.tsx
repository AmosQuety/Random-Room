import type { ReactNode } from "react";
import { CheckIcon } from "../../components/icons";
import { Eyebrow } from "../../components/ui";
import { winnersOf } from "./winners";
import type { RoundScore } from "./types";

/** Who has acted this round (never what they did), in words as well as icons. */
export function ProgressChips({ done, verb = "answered" }: { done: Record<string, boolean>; verb?: string }) {
  const names = Object.keys(done);
  const count = names.filter((n) => done[n]).length;
  return (
    <div>
      <p aria-live="polite" className="text-sm text-muted">
        {count} of {names.length} {verb}
      </p>
      <ul aria-label="Answer progress" className="mt-2 flex flex-wrap gap-2">
        {names.map((name) => (
          <li
            key={name}
            className={`inline-flex min-h-8 items-center gap-1.5 rounded-full border-2 border-ink px-3 text-sm font-semibold ${done[name] ? "bg-leaf-soft" : "bg-paper"}`}
          >
            {done[name] ? <CheckIcon className="size-3.5" /> : <span aria-hidden="true" className="size-2 rounded-full bg-muted" />}
            {name}
            <span className="sr-only">{done[name] ? ` has ${verb}` : ` has not ${verb} yet`}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function GameOver({ scoreboard }: { scoreboard: RoundScore[] }) {
  const winners = winnersOf(scoreboard);
  return (
    <section className="surface-dark rounded-xl border-2 border-ink bg-ink p-6 text-center text-paper shadow-ticket motion-safe:animate-stamp">
      <p className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">Game over</p>
      <p className="mt-2 font-display text-3xl font-black sm:text-4xl">
        {winners.length === 0 ? "Nobody scored" : winners.length === 1 ? `${winners[0]} wins` : `${winners.join(" & ")} tie`}
      </p>
    </section>
  );
}

/** The host's in-game buttons, set apart from what every player sees. */
export function HostBar({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center gap-3 border-t-2 border-dashed border-ink pt-4">
      <Eyebrow>Host</Eyebrow>
      {children}
    </div>
  );
}
