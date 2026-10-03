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
}

export function HostControls({ status, busy, onStart, onEnd, onNewRound }: Props) {
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
        </>
      )}
    </section>
  );
}
