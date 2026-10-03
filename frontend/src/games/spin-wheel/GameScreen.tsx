import { useEffect, useState } from "react";
import { FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar } from "../rounds/parts";
import type { GameScreenProps } from "../types";
import type { WheelPayload } from "./types";
import { Wheel } from "./Wheel";

const SPIN_MS = 4000;

const reducedMotion = () => typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;

/**
 * Holds the result text back until the disc has stopped, unless the page loaded with the result already there.
 * Turn is remounted per spin (key), so each spin starts un-landed.
 */
function useLanded(hasResult: boolean): boolean {
  const [landed, setLanded] = useState(hasResult);
  useEffect(() => {
    if (!hasResult || landed) return;
    const timer = setTimeout(() => setLanded(true), reducedMotion() ? 0 : SPIN_MS);
    return () => clearTimeout(timer);
  }, [hasResult, landed]);
  return landed;
}

function Turn({ payload, me, isHost, live, busy, onAction }: { payload: WheelPayload; me: string; isHost: boolean; live: boolean; busy: boolean; onAction: GameScreenProps<WheelPayload>["onAction"] }) {
  const landed = useLanded(payload.last !== null);
  const myTurn = payload.spinner === me;
  const lastSpin = payload.round >= payload.totalRounds;

  return (
    <section aria-labelledby="turn-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
      <p id="turn-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
        Spin {payload.round} of {payload.totalRounds}
      </p>
      <p className="font-display text-2xl font-black sm:text-3xl">{myTurn ? "Your turn to spin" : `${payload.spinner}'s turn to spin`}</p>

      <Wheel key={payload.round} segments={payload.segments} target={payload.last?.index ?? null} />

      {payload.phase === "ready" && (
        <Button variant="accent" size="lg" disabled={!myTurn || !live || busy} onClick={() => onAction("spin")}>
          <PlayIcon className="size-4" /> {myTurn ? "Spin the wheel" : `Waiting for ${payload.spinner}`}
        </Button>
      )}

      <p role="status" aria-live="polite" className={landed && payload.last ? "rounded-lg border-2 border-ink bg-accent-soft px-4 py-3 font-display text-xl font-black motion-safe:animate-stamp" : "sr-only"}>
        {payload.last ? `${payload.last.player} landed on: ${payload.last.label}` : ""}
      </p>

      {payload.phase === "spun" && landed && payload.awarded && <p className="font-bold">Point awarded to {payload.last?.player}.</p>}

      <details className="text-sm">
        <summary className="min-h-11 cursor-pointer py-2 font-semibold">Everything on the wheel</summary>
        <ol className="list-decimal pl-6">
          {payload.segments.map((s, i) => (
            <li key={i}>{s}</li>
          ))}
        </ol>
      </details>

      {isHost && live && payload.phase === "spun" && (
        <HostBar>
          <Button variant="secondary" size="sm" disabled={busy || payload.awarded || !landed} onClick={() => onAction("award")}>
            Award a point
          </Button>
          <Button variant="accent" size="sm" disabled={busy || !landed} onClick={() => onAction("next")}>
            {lastSpin ? <FlagIcon className="size-4" /> : <PlayIcon className="size-4" />}
            {lastSpin ? "Finish game" : "Next spin"}
          </Button>
        </HostBar>
      )}
    </section>
  );
}

export function SpinWheelGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<WheelPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";

  return (
    <>
      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Take turns on the wheel</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start round below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {(payload.phase === "ready" || payload.phase === "spun") && !over && (
        <Turn key={payload.round} payload={payload} me={me} isHost={isHost} live={live} busy={busy} onAction={onAction} />
      )}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="points" />
    </>
  );
}
