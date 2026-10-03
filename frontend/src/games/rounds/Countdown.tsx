interface Props {
  secondsLeft: number;
  totalSeconds: number;
}

const WARN_AT = [10, 5];

function clock(seconds: number): string {
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

/** A visible and spoken countdown. The bar never animates, so reduced-motion users see the same thing. */
export function Countdown({ secondsLeft, totalSeconds }: Props) {
  const percent = totalSeconds > 0 ? Math.round((secondsLeft / totalSeconds) * 100) : 0;
  const urgent = secondsLeft <= 5;
  return (
    <div className="flex items-center gap-3">
      <span
        role="timer"
        aria-label="Time left"
        className={`font-mono text-2xl font-black tabular-nums ${urgent ? "text-tomato" : "text-ink"}`}
      >
        {clock(secondsLeft)}
      </span>
      <div aria-hidden="true" className="h-2.5 w-24 overflow-hidden rounded-full border-2 border-ink bg-paper sm:w-40">
        <div className={`h-full ${urgent ? "bg-tomato" : "bg-accent"}`} style={{ width: `${percent}%` }} />
      </div>
      <span role="status" className="sr-only">
        {WARN_AT.includes(secondsLeft) ? `${secondsLeft} seconds left` : ""}
      </span>
    </div>
  );
}
