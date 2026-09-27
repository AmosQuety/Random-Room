import type { SessionStatus } from "../lib/types";

interface Props {
  status: SessionStatus;
  busy: boolean;
  onStart: () => void;
  onEnd: () => void;
  onNewRound: () => void;
}

const base =
  "min-h-12 rounded-lg border-2 border-ink px-5 font-black uppercase tracking-wide shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:opacity-50";

export function HostControls({ status, busy, onStart, onEnd, onNewRound }: Props) {
  return (
    <section aria-label="Host controls" className="flex flex-wrap items-center gap-3 rounded-xl border-2 border-dashed border-ink px-4 py-3">
      <p className="font-mono text-xs uppercase tracking-widest text-muted">Host controls</p>
      {status === "Waiting" && (
        <button type="button" disabled={busy} onClick={onStart} className={`${base} bg-leaf text-white`}>
          Start round
        </button>
      )}
      {status === "Active" && (
        <button type="button" disabled={busy} onClick={onEnd} className={`${base} bg-ink text-paper`}>
          🏁 End round
        </button>
      )}
      {status === "Completed" && (
        <button type="button" disabled={busy} onClick={onNewRound} className={`${base} bg-mustard text-ink`}>
          Start new round
        </button>
      )}
    </section>
  );
}
