import { useState } from "react";
import { ApiError, endRound, startNewRound, startRound, triggerRandom } from "../lib/api";
import type { RoomSnapshot, Session } from "../lib/types";
import { useRoom } from "../lib/useRoom";
import { ActivityTimeline } from "./ActivityTimeline";
import { FinalResult } from "./FinalResult";
import { HostControls } from "./HostControls";
import { PlayerCard } from "./PlayerCard";

const STATUS_COPY = {
  Waiting: "Waiting for the host to start",
  Active: "Round is live",
  Completed: "Round complete",
} as const;

interface Props {
  session: Session;
  onLeave: () => void;
}

export function RoomScreen({ session, onLeave }: Props) {
  const { snapshot, connection, applySnapshot } = useRoom(session.token, onLeave);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run(action: (token: string) => Promise<RoomSnapshot>) {
    setBusy(true);
    setError(null);
    try {
      applySnapshot(await action(session.token));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.");
    } finally {
      setBusy(false);
    }
  }

  if (!snapshot) return <p className="p-6 font-mono text-muted">Loading the room...</p>;

  const { round, players } = snapshot;

  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-8 px-4 py-8">
      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2 font-mono text-xs uppercase tracking-widest text-muted">
          <span>
            Round {round.number} · {STATUS_COPY[round.status]}
          </span>
          <span role="status">{connection === "live" ? "🟢 Live" : "🟠 Reconnecting..."}</span>
        </div>
        <h1 className="font-display text-4xl font-black leading-none sm:text-6xl">{snapshot.roomTitle}</h1>
        <p className="text-lg text-muted">
          The system picks between{" "}
          {snapshot.choices.map((choice, i) => (
            <span key={choice}>
              {i > 0 && (i === snapshot.choices.length - 1 ? " and " : ", ")}
              <strong className="text-ink">{choice}</strong>
            </span>
          ))}
          . Nobody chooses for themselves.
        </p>
        <p className="text-sm text-muted">
          You are <strong className="text-ink">{session.player}</strong>
          {session.isHost && " (host)"} ·{" "}
          <button type="button" onClick={onLeave} className="underline">
            Not you?
          </button>
        </p>
      </header>

      {error && (
        <p role="alert" className="rounded-lg border-2 border-tomato bg-card px-4 py-3 font-semibold text-tomato">
          {error}
        </p>
      )}

      {round.status === "Completed" && <FinalResult snapshot={snapshot} />}

      <section aria-labelledby="participants-heading">
        <h2 id="participants-heading" className="mb-3 font-mono text-sm uppercase tracking-widest">
          Participants
        </h2>
        <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {players.map((p) => (
            <PlayerCard
              key={p.name}
              player={p}
              isMe={p.name === session.player}
              canTrigger={round.status === "Active" && !p.hasTriggered}
              rolling={busy}
              onTrigger={() => run(triggerRandom)}
            />
          ))}
        </ul>
      </section>

      {session.isHost && (
        <HostControls
          status={round.status}
          busy={busy}
          onStart={() => run(startRound)}
          onEnd={() => run(endRound)}
          onNewRound={() => run(startNewRound)}
        />
      )}

      <ActivityTimeline activity={snapshot.activity} />
    </main>
  );
}
