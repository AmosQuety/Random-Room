import type { SessionStatus } from "../lib/types";
import { FlagIcon, PlayIcon, PlusIcon } from "./icons";
import { ConfirmButton } from "./ConfirmButton";
import { Button, Eyebrow } from "./ui";

interface Props {
  status: SessionStatus;
  busy: boolean;
  onStart: () => void;
  onEnd: () => void;
  onNewRound: () => void;
  hostPlays: boolean;
  /** The game always needs a referee (Buzzer), so the host cannot choose to play. */
  requiresReferee: boolean;
  onHostPlaysChange: (plays: boolean) => void;
}

export function HostControls({ status, busy, onStart, onEnd, onNewRound, hostPlays, requiresReferee, onHostPlaysChange }: Props) {
  return (
    <section aria-label="Host controls" className="flex flex-wrap items-center gap-3 rounded-xl border-2 border-dashed border-ink px-4 py-3">
      <Eyebrow>Host controls</Eyebrow>
      {status === "Waiting" && (
        <Button variant="accent" disabled={busy} onClick={onStart}>
          <PlayIcon className="size-4" /> Start game
        </Button>
      )}
      {status === "Active" && (
        <ConfirmButton
          variant="dark"
          disabled={busy}
          question="End the game for everyone?"
          confirmLabel="Yes, end the game"
          cancelLabel="Keep playing"
          onConfirm={onEnd}
        >
          <FlagIcon className="size-4" /> End game
        </ConfirmButton>
      )}
      {status === "Completed" && (
        <>
          <Button variant="secondary" disabled={busy} onClick={onNewRound}>
            <PlusIcon className="size-4" /> Start new game
          </Button>
          <span className="text-sm text-muted">Scores start again from zero.</span>
          <HostPlaysToggle checked={hostPlays && !requiresReferee} disabled={busy || requiresReferee} requiresReferee={requiresReferee} onChange={onHostPlaysChange} />
        </>
      )}
    </section>
  );
}

interface ToggleProps {
  checked: boolean;
  disabled: boolean;
  requiresReferee: boolean;
  onChange: (plays: boolean) => void;
}

// Only offered between games: the server refuses it during one, because a game is built from who plays.
function HostPlaysToggle({ checked, disabled, requiresReferee, onChange }: ToggleProps) {
  const hint = requiresReferee
    ? "In this game the host always judges, so the host cannot play."
    : "This applies from the next game, not the one on screen. If you play, you appear on the scoreboard, another player gives you points in Spin the Wheel, and in Forbidden Words you can flag a slip only when you are the judge.";
  return (
    <div className="basis-full">
      <label className="flex items-center gap-2 font-semibold">
        <input
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(e) => onChange(e.target.checked)}
          aria-describedby="host-plays-hint"
          className="size-5 shrink-0 accent-[var(--accent)]"
        />
        Play in the next game
      </label>
      <p id="host-plays-hint" className="mt-1 text-sm text-muted">
        {hint}
      </p>
    </div>
  );
}
