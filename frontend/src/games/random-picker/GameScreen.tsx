import type { GameScreenProps } from "../types";
import { ActivityTimeline } from "./ActivityTimeline";
import { FinalResult } from "./FinalResult";
import { PlayerCard } from "./PlayerCard";
import type { RandomPickerPayload } from "./types";

export function RandomPickerGameScreen({ me, players, session, payload, busy, onAction }: GameScreenProps<RandomPickerPayload>) {
  const merged = players.map((p) => ({
    ...p,
    ...(payload.players[p.name] ?? { hasTriggered: false, result: null }),
  }));

  return (
    <>
      <p className="text-lg text-muted">
        The system picks between{" "}
        {payload.choices.map((choice, i) => (
          <span key={choice}>
            {i > 0 && (i === payload.choices.length - 1 ? " and " : ", ")}
            <strong className="text-ink">{choice}</strong>
          </span>
        ))}
        . Nobody chooses for themselves.
      </p>

      {session.status === "Completed" && <FinalResult roundNumber={session.number} tally={payload.tally} />}

      <section aria-labelledby="participants-heading">
        <h2 id="participants-heading" className="mb-3 font-mono text-sm uppercase tracking-widest">
          Participants
        </h2>
        <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {merged.map((p) => (
            <PlayerCard
              key={p.name}
              player={p}
              isMe={p.name === me}
              canTrigger={session.status === "Active" && !p.hasTriggered}
              rolling={busy}
              onTrigger={() => onAction("trigger")}
            />
          ))}
        </ul>
      </section>

      <ActivityTimeline activity={payload.activity} />
    </>
  );
}
