import { formatTime } from "../../lib/format";
import type { ActivityView } from "./types";

export function ActivityTimeline({ activity }: { activity: ActivityView[] }) {
  return (
    <section aria-labelledby="activity-heading">
      <h2 id="activity-heading" className="font-mono text-sm uppercase tracking-widest">
        Activity
      </h2>
      {activity.length === 0 ? (
        <p className="mt-3 text-muted">No random decisions yet.</p>
      ) : (
        <ol aria-live="polite" className="mt-3 flex flex-col gap-2">
          {activity.map((a) => (
            <li key={a.eventId} className="rounded-lg border-2 border-ink bg-card px-3 py-2">
              <p className="font-mono text-xs text-muted">
                {formatTime(a.timestamp)} · round {a.sessionNumber}
              </p>
              <p>
                🎲 <strong>{a.triggeredBy}</strong> triggered random selection
              </p>
              <p>
                System selected <strong className="font-display text-lg text-tomato">{a.result}</strong>
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
