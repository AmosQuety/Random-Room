import { Suspense, useEffect, useRef, useState } from "react";
import { GAMES } from "../games/registry";
import { ApiError, deleteRoom, endSession, makeRecoveryCode, performAction, resetPin, startNewSession, startSession } from "../lib/api";
import type { RoomSnapshot, Session, SessionStatus } from "../lib/types";
import { currentError, type RoomError } from "../lib/snapshots";
import { useRoom, type ConnectionStatus, type SessionEnd } from "../lib/useRoom";
import { Avatar } from "./Avatar";
import { HostControls } from "./HostControls";
import { DeleteRoomControl } from "./DeleteRoomControl";
import { SeatControls } from "./SeatControls";
import { Shell } from "./Shell";
import { Alert, Button, EmptyState, Eyebrow, Skeleton } from "./ui";

const STATUS_COPY: Record<SessionStatus, string> = {
  Waiting: "Waiting for the host to start",
  Active: "Game is live",
  Completed: "Game complete",
};

const STATUS_SHORT: Record<SessionStatus, string> = {
  Waiting: "Waiting",
  Active: "Live",
  Completed: "Complete",
};

const STATUS_STYLE: Record<SessionStatus, string> = {
  Waiting: "bg-paper-deep text-ink",
  Active: "bg-accent text-on-accent",
  Completed: "bg-ink text-paper",
};

/** Sent by game screens as their countdown reaches zero. A background nudge, not something the player did. */
const TIMER_TICK = "tick";

interface Props {
  session: Session;
  onLeave: () => void;
  /** The server no longer accepts this player's token; the app returns to the join screen and says why. */
  onSessionExpired: (reason: SessionEnd) => void;
}

const isUnauthorized = (e: unknown) => e instanceof ApiError && e.status === 401;

function ConnectionPill({ connection }: { connection: ConnectionStatus }) {
  const live = connection === "live";
  return (
    <span role="status" className="inline-flex items-center gap-2 font-mono text-xs font-bold uppercase tracking-widest">
      <span
        aria-hidden="true"
        className={`size-3 rounded-full border-2 border-ink ${live ? "bg-leaf" : "bg-mustard motion-safe:animate-shimmer"}`}
      />
      {live ? "Live" : connection === "connecting" ? "Connecting" : "Reconnecting"}
    </span>
  );
}

function RoomSkeleton() {
  return (
    <div role="status" aria-label="Loading the room" className="flex flex-col gap-6">
      <Skeleton className="h-6 w-40" />
      <Skeleton className="h-16 w-3/4" />
      <Skeleton className="h-48" />
      <Skeleton className="h-32" />
    </div>
  );
}

export function RoomScreen({ session, onLeave, onSessionExpired }: Props) {
  const { snapshot, connection, loadFailed, retry, applySnapshot } = useRoom(session.token, onSessionExpired);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<RoomError | null>(null);

  // The failure is tied to the snapshot it happened on; see currentError.
  const latestSequence = useRef(0);
  useEffect(() => {
    latestSequence.current = snapshot?.sequence ?? 0;
  }, [snapshot]);

  async function run(action: (token: string) => Promise<RoomSnapshot>) {
    setBusy(true);
    setError(null);
    try {
      applySnapshot(await action(session.token));
    } catch (e) {
      // An expired token ends the session the same way it does when the room is loaded: back to the join screen.
      if (isUnauthorized(e)) return onSessionExpired("expired");
      const message = e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.";
      setError({ message, sequence: latestSequence.current });
    } finally {
      setBusy(false);
    }
  }

  // Timer ticks must not lock the buttons or show errors: one that arrives after the round has already ended is
  // expected, and the next push or refetch corrects the screen anyway.
  async function nudge(action: (token: string) => Promise<RoomSnapshot>) {
    try {
      applySnapshot(await action(session.token));
    } catch (e) {
      if (isUnauthorized(e)) onSessionExpired("expired");
      // Anything else is deliberately ignored, see above.
    }
  }

  async function resetSeat(player: string) {
    try {
      return await resetPin(session.token, player);
    } catch (e) {
      if (isUnauthorized(e)) onSessionExpired("expired");
      throw e;
    }
  }

  async function newRecoveryCode() {
    try {
      return (await makeRecoveryCode(session.token)).recoveryCode;
    } catch (e) {
      if (isUnauthorized(e)) onSessionExpired("expired");
      throw e;
    }
  }

  async function removeRoom() {
    try {
      await deleteRoom(session.token);
    } catch (e) {
      if (isUnauthorized(e)) onSessionExpired("expired");
      throw e;
    }
    onSessionExpired("deleted");
  }

  const headerRight = (
    <>
      <ConnectionPill connection={connection} />
      <span className="hidden items-center gap-2 sm:flex">
        <Avatar name={session.player} />
        <span className="truncate font-bold">{session.player}</span>
      </span>
    </>
  );

  if (!snapshot) {
    return (
      <Shell width="wide" headerRight={headerRight}>
        {loadFailed ? (
          <EmptyState
            tone="error"
            title="Couldn't load the room"
            action={
              <>
                <Button variant="primary" onClick={retry}>
                  Try again
                </Button>
                <Button variant="secondary" onClick={onLeave}>
                  Leave room
                </Button>
              </>
            }
          >
            <p>Check your connection. Your place in the room is safe.</p>
          </EmptyState>
        ) : (
          <RoomSkeleton />
        )}
      </Shell>
    );
  }

  const errorMessage = currentError(error, snapshot);
  const game = GAMES[snapshot.gameType];
  if (!game) {
    return (
      <Shell width="wide" headerRight={headerRight}>
        <EmptyState
          tone="error"
          title="Unknown game type"
          action={
            <Button variant="secondary" onClick={onLeave}>
              Leave room
            </Button>
          }
        >
          <p>This room uses "{snapshot.gameType}", which this version of the app doesn't know. Try refreshing the page.</p>
        </EmptyState>
      </Shell>
    );
  }

  const { GameScreen } = game;
  const { status, number } = snapshot.session;

  return (
    <Shell width="wide" accent={game.accent} headerRight={headerRight}>
      <div className="flex flex-col gap-8">
        {/* Announces round changes to screen readers without moving focus. */}
        <p role="status" className="sr-only">
          Game {number}: {STATUS_COPY[status]}
        </p>

        <header className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <Eyebrow className="flex items-center gap-2">
              <game.Glyph className="size-6" />
              {game.name}
            </Eyebrow>
            <span
              className={`rounded-md border-2 border-ink px-2 py-1 font-mono text-xs font-bold uppercase tracking-widest ${STATUS_STYLE[status]}`}
            >
              Game {number} · {STATUS_SHORT[status]}
            </span>
          </div>
          <h1 className="font-display text-4xl font-black leading-[0.95] tracking-tight sm:text-6xl">{snapshot.roomTitle || "Game room"}</h1>
          <p className="flex flex-wrap items-center gap-x-2 text-sm text-muted">
            <span>
              You are <strong className="text-ink">{session.player}</strong>
              {session.isHost && " (host)"}
            </span>
            <span aria-hidden="true">·</span>
            <button type="button" onClick={onLeave} className="-my-2 min-h-11 rounded-lg px-1 underline">
              Not you?
            </button>
          </p>
        </header>

        {connection === "reconnecting" && (
          <p role="status" className="rounded-lg border-2 border-ink bg-mustard-soft px-4 py-3 font-semibold">
            Connection lost - reconnecting. Anything you've already played is safe on the server.
          </p>
        )}

        {errorMessage && <Alert>{errorMessage}</Alert>}

        <Suspense fallback={<RoomSkeleton />}>
          <GameScreen
            me={session.player}
            isHost={session.isHost}
            players={snapshot.players}
            session={snapshot.session}
            payload={snapshot.gamePayload}
            busy={busy}
            onAction={(action, payload) => (action === TIMER_TICK ? nudge : run)((token) => performAction(token, action, payload))}
          />
        </Suspense>

        {session.isHost && (
          <HostControls
            status={status}
            busy={busy}
            onStart={() => run(startSession)}
            onEnd={() => run(endSession)}
            onNewRound={() => run(startNewSession)}
          />
        )}

        {session.isHost && (
          <SeatControls
            slug={snapshot.roomSlug}
            roomTitle={snapshot.roomTitle}
            hostPlayer={snapshot.hostPlayer}
            players={snapshot.players}
            status={status}
            onReset={resetSeat}
            onNewRecoveryCode={newRecoveryCode}
          />
        )}

        {session.isHost && <DeleteRoomControl onDelete={removeRoom} />}
      </div>
    </Shell>
  );
}
