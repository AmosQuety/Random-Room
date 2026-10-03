import { useState } from "react";
import { ApiError } from "../lib/api";
import { claimUrl } from "../lib/invites";
import type { PlayerInvite, PlayerView, SessionStatus } from "../lib/types";
import { ConfirmButton } from "./ConfirmButton";
import { InviteActions } from "./InviteActions";
import { Alert, Eyebrow } from "./ui";

interface Props {
  slug: string;
  roomTitle: string;
  hostPlayer: string;
  players: PlayerView[];
  status: SessionStatus;
  onReset: (player: string) => Promise<PlayerInvite>;
}

/**
 * For a player who forgot their PIN, changed phone, or lost their link: the host empties the seat and sends a fresh
 * one-time link. Hidden inside a details element because most games never need it.
 */
export function SeatControls({ slug, roomTitle, hostPlayer, players, status, onReset }: Props) {
  const [links, setLinks] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const gameRunning = status === "Active";
  const seats = players.filter((p) => p.name !== hostPlayer);

  // A link is only useful until its owner has used it, so it is shown only while the seat is still empty.
  const linkFor = (seat: PlayerView) => (seat.claimed ? undefined : links[seat.name]);

  async function reset(player: string) {
    setError(null);
    try {
      const invite = await onReset(player);
      setLinks((current) => ({ ...current, [player]: invite.inviteToken }));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.");
    }
  }

  return (
    <details className="rounded-xl border-2 border-dashed border-ink px-4 py-3">
      <summary className="min-h-11 cursor-pointer py-2 font-bold">Someone locked out? Reset a seat</summary>
      <div className="mt-2 flex flex-col gap-4">
        <p className="text-sm text-muted">
          A reset signs the player out of their old device and gives you a new one-time link to send them. They choose a
          new PIN. You can only do this between games.
          {gameRunning && <strong className="text-ink"> End the game first.</strong>}
        </p>
        {error && <Alert>{error}</Alert>}
        <ul className="flex flex-col gap-3">
          {seats.map((seat) => (
            <li key={seat.name} className="flex flex-col gap-2 rounded-lg border-2 border-ink bg-paper px-4 py-3">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="font-display text-lg font-bold">
                  {seat.name}
                  <span className="ml-2 font-mono text-xs font-normal uppercase tracking-widest text-muted">
                    {seat.claimed ? "ready" : "not joined"}
                  </span>
                </p>
                <ConfirmButton
                  disabled={gameRunning}
                  question={`Reset ${seat.name}'s seat? Their current device is signed out.`}
                  confirmLabel="Yes, reset"
                  cancelLabel="Cancel"
                  onConfirm={() => void reset(seat.name)}
                >
                  {seat.claimed ? "Reset PIN" : "Get a new link"}{" "}
                  <span className="sr-only">for {seat.name}</span>
                </ConfirmButton>
              </div>
              {linkFor(seat) && (
                <div className="flex flex-col gap-2">
                  <Eyebrow>New link for {seat.name}</Eyebrow>
                  <p className="truncate font-mono text-xs text-muted">{claimUrl(slug, linkFor(seat)!)}</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <InviteActions slug={slug} roomTitle={roomTitle} player={seat.name} token={linkFor(seat)!} />
                  </div>
                </div>
              )}
            </li>
          ))}
        </ul>
        <p className="text-sm text-muted">Your own seat cannot be reset. If you forget your PIN, start a new room.</p>
      </div>
    </details>
  );
}
