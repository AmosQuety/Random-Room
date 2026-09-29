import type { SessionStatus } from "../lib/types";
import { FlagIcon, PlayIcon, PlusIcon } from "./icons";
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
          <PlayIcon className="size-4" /> Start round
        </Button>
      )}
      {status === "Active" && (
        <Button variant="dark" disabled={busy} onClick={onEnd}>
          <FlagIcon className="size-4" /> End round
        </Button>
      )}
      {status === "Completed" && (
        <Button variant="secondary" disabled={busy} onClick={onNewRound}>
          <PlusIcon className="size-4" /> Start new round
        </Button>
      )}
    </section>
  );
}
