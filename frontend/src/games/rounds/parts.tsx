import type { ReactNode } from "react";
import { CheckIcon } from "../../components/icons";
import { Eyebrow } from "../../components/ui";
import { unjoinedNames } from "../../lib/players";
import { winnersOf } from "./winners";
import type { HostScoreNote, RoundScore } from "./types";

/** The host's decisions that changed (or withheld) a score, in plain words, so nobody has to wonder where a point came from. */
export function HostScoreNotes({ notes }: { notes?: readonly HostScoreNote[] }) {
  if (!notes || notes.length === 0) return null;
  const newestFirst = [...notes].reverse();
  return (
    <section aria-label="Host decisions about scores" className="rounded-lg border-2 border-dashed border-ink px-4 py-3">
      <p className="font-mono text-xs font-bold uppercase tracking-widest text-muted">Host decisions about scores</p>
      <ul className="mt-2 flex flex-col gap-1 text-sm">
        {newestFirst.map((n, i) => (
          <li key={`${n.round}-${i}`}>
            <span className="font-mono text-xs text-muted">Round {n.round}</span> {n.player}{" "}
            <strong>{n.points === 0 ? "no points" : `${n.points > 0 ? "+" : "-"}${Math.abs(n.points)}`}</strong>: {n.reason}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** Said under a "waiting for X" line when X has not opened their invite link, so the group knows why nothing happens. */
export function NotJoinedNote({ name, players }: { name: string | null; players: readonly { name: string; claimed: boolean }[] }) {
  if (name === null || !unjoinedNames(players).includes(name)) return null;
  return <p className="text-sm font-semibold">{name} has not opened their invite link yet.</p>;
}

/** Who has acted this round (never what they did), in words as well as icons. */
export function ProgressChips({ done, verb = "answered", notJoined = [] }: { done: Record<string, boolean>; verb?: string; notJoined?: readonly string[] }) {
  const names = Object.keys(done);
  const count = names.filter((n) => done[n]).length;
  return (
    <div>
      <p aria-live="polite" className="text-sm text-muted">
        {count} of {names.length} {verb}
      </p>
      {notJoined.length > 0 && (
        <p className="mt-1 text-sm font-semibold">Waiting for {notJoined.join(", ")} to open their invite link.</p>
      )}
      <ul aria-label="Answer progress" className="mt-2 flex flex-wrap gap-2">
        {names.map((name) => (
          <li
            key={name}
            className={`inline-flex min-h-8 items-center gap-1.5 rounded-full border-2 border-ink px-3 text-sm font-semibold ${done[name] ? "bg-leaf-soft" : "bg-paper"}`}
          >
            {done[name] ? <CheckIcon className="size-3.5" /> : <span aria-hidden="true" className="size-2 rounded-full bg-muted" />}
            {name}
            {notJoined.includes(name) && <span className="font-mono text-xs uppercase text-muted">not joined</span>}
            <span className="sr-only">{notJoined.includes(name) ? " has not joined yet" : done[name] ? ` has ${verb}` : ` has not ${verb} yet`}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** teamName lets a team game announce its winning team; without it the banner names the top player or players. */
export function GameOver({ scoreboard, teamName }: { scoreboard: RoundScore[]; teamName?: string }) {
  const winners = winnersOf(scoreboard);
  return (
    <section className="surface-dark rounded-xl border-2 border-ink bg-ink p-6 text-center text-paper shadow-ticket motion-safe:animate-stamp">
      <p className="font-mono text-xs uppercase tracking-[0.3em] text-mustard">Game over</p>
      <p className="mt-2 font-display text-3xl font-black sm:text-4xl">
        {teamName && winners.length > 0
          ? `${teamName} team wins: ${winners.join(" & ")}`
          : winners.length === 0
            ? "Nobody scored"
            : winners.length === 1
              ? `${winners[0]} wins`
              : `${winners.join(" & ")} tie`}
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
