import { useState } from "react";
import { GAMES } from "../games/registry";
import { ApiError, endSession, performAction, startNewSession, startSession } from "../lib/api";
import type { RoomSnapshot, Session } from "../lib/types";
import { useRoom } from "../lib/useRoom";
import { HostControls } from "./HostControls";

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

  const game = GAMES[snapshot.gameType];
  if (!game) return <p className="p-6 font-mono text-tomato">Unknown game type "{snapshot.gameType}".</p>;

  const { GameScreen } = game;

  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-8 px-4 py-8">
      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2 font-mono text-xs uppercase tracking-widest text-muted">
          <span>
            Round {snapshot.session.number} · {STATUS_COPY[snapshot.session.status]}
          </span>
          <span role="status">{connection === "live" ? "🟢 Live" : "🟠 Reconnecting..."}</span>
        </div>
        <h1 className="font-display text-4xl font-black leading-none sm:text-6xl">{snapshot.roomTitle}</h1>
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

      <GameScreen
        me={session.player}
        isHost={session.isHost}
        players={snapshot.players}
        session={snapshot.session}
        payload={snapshot.gamePayload}
        busy={busy}
        onAction={(action, payload) => run((token) => performAction(token, action, payload))}
      />

      {session.isHost && (
        <HostControls
          status={snapshot.session.status}
          busy={busy}
          onStart={() => run(startSession)}
          onEnd={() => run(endSession)}
          onNewRound={() => run(startNewSession)}
        />
      )}
    </main>
  );
}
