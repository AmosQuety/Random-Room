import { CheckIcon, FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar } from "../rounds/parts";
import type { GameScreenProps } from "../types";
import { FREE_CELL, type BingoPayload } from "./types";

interface CellProps {
  label: string;
  index: number;
  marked: boolean;
  called: boolean;
  inWinningLine: boolean;
  disabled: boolean;
  onMark: () => void;
}

function Cell({ label, index, marked, called, inWinningLine, disabled, onMark }: CellProps) {
  const free = index === FREE_CELL;
  const tone = marked ? (inWinningLine ? "bg-leaf text-white" : "bg-accent text-on-accent") : called ? "bg-accent-soft" : "bg-paper";
  const name = free ? "Free space" : label;
  const state = marked ? "marked" : called ? "called, not marked" : "not called";
  return (
    <button
      type="button"
      aria-pressed={marked}
      aria-label={`${name}, ${state}`}
      disabled={disabled || marked || !called}
      onClick={onMark}
      className={`relative grid min-h-14 place-items-center rounded-md border-2 border-ink p-1 text-center text-[0.7rem] font-bold leading-tight min-w-0 [overflow-wrap:anywhere] transition disabled:cursor-default sm:min-h-20 sm:text-sm ${tone} ${called && !marked ? "ring-2 ring-ink ring-offset-1" : ""}`}
    >
      {free ? "FREE" : label}
      {marked && !free && <CheckIcon aria-hidden="true" className="absolute right-0.5 top-0.5 size-3" />}
    </button>
  );
}

function Card({ payload, live, busy, onAction }: { payload: BingoPayload; live: boolean; busy: boolean; onAction: GameScreenProps<BingoPayload>["onAction"] }) {
  if (!payload.myCard || !payload.myMarks) return null;
  const calledSet = new Set(payload.called);
  return (
    <div role="group" aria-label="Your bingo card" className="grid grid-cols-5 gap-1 sm:gap-2">
      {payload.myCard.map((label, i) => (
        <Cell
          key={i}
          label={label}
          index={i}
          marked={payload.myMarks![i]}
          called={i === FREE_CELL || calledSet.has(label)}
          inWinningLine={payload.winningLine?.includes(i) ?? false}
          disabled={!live || busy || payload.phase !== "calling"}
          onMark={() => onAction("mark", { cell: i })}
        />
      ))}
    </div>
  );
}

export function BingoGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<BingoPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const latest = payload.called.at(-1) ?? null;
  const allCalled = payload.called.length >= payload.poolSize;

  return (
    <>
      <p role="status" aria-live="polite" className="sr-only">
        {latest && !over ? `Called: ${latest}` : ""}
        {payload.winner ? `${payload.winner} got bingo` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Get your cards</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start game below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.phase === "calling" && (
        <section aria-labelledby="called-heading" className="flex flex-col gap-4 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <p id="called-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
            Called {payload.called.length} of {payload.poolSize}
          </p>
          <p className="font-display text-4xl font-black leading-tight sm:text-5xl">{latest ?? "Waiting for the first call"}</p>
          {payload.called.length > 1 && <p className="text-sm text-muted">Earlier: {payload.called.slice(0, -1).reverse().join(", ")}</p>}
        </section>
      )}

      {payload.myCard && (
        <section aria-labelledby="card-heading" className="flex flex-col gap-4 rounded-xl border-2 border-ink bg-card p-4 shadow-ticket sm:p-6">
          <h3 id="card-heading" className="font-display text-xl font-black">
            Your card
          </h3>
          <Card payload={payload} live={live} busy={busy} onAction={onAction} />
          {payload.phase === "calling" && live && (
            <Button variant="accent" size="lg" disabled={busy} onClick={() => onAction("bingo")}>
              Bingo!
            </Button>
          )}
        </section>
      )}

      {isHost && live && payload.phase === "calling" && (
        <HostBar>
          <Button variant="accent" size="sm" disabled={busy || allCalled} onClick={() => onAction("call")}>
            <PlayIcon className="size-4" /> {allCalled ? "Everything called" : "Call next item"}
          </Button>
          <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("end")}>
            <FlagIcon className="size-4" /> End game
          </Button>
        </HostBar>
      )}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="wins" />
    </>
  );
}
