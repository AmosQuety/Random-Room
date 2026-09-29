import { Avatar } from "../../components/Avatar";
import { CheckIcon, DiceIcon } from "../../components/icons";
import { PresenceDot } from "../../components/ui";

/** A room player merged with their Random Picker state for this session. */
export interface RandomPickerPlayer {
  name: string;
  online: boolean;
  hasTriggered: boolean;
  result: string | null;
}

interface Props {
  player: RandomPickerPlayer;
  isMe: boolean;
  canTrigger: boolean;
  rolling: boolean;
  onTrigger: () => void;
}

export function PlayerCard({ player, isMe, canTrigger, rolling, onTrigger }: Props) {
  return (
    <li
      className={`flex flex-col gap-4 rounded-xl border-2 border-ink bg-card p-4 shadow-ticket ${isMe ? "ring-4 ring-mustard/60" : ""}`}
    >
      <div className="flex items-center gap-3">
        <Avatar name={player.name} />
        <div className="min-w-0">
          <p className="truncate font-display text-xl font-bold">
            {player.name}
            {isMe && <span className="ml-2 font-mono text-xs font-normal uppercase text-muted">(you)</span>}
          </p>
          <PresenceDot online={player.online} />
        </div>
      </div>

      <CardBody player={player} canTrigger={canTrigger} rolling={rolling} isMe={isMe} onTrigger={onTrigger} />
    </li>
  );
}

function CardBody({ player, canTrigger, rolling, isMe, onTrigger }: Props) {
  if (player.result) {
    return (
      <div key={player.result} className="animate-stamp rounded-lg border-2 border-ink bg-accent-soft px-3 py-4 text-center">
        <p className="flex items-center justify-center gap-1.5 font-mono text-xs font-bold uppercase tracking-widest text-ink">
          <DiceIcon className="size-4" /> System random result
        </p>
        <p className="mt-1 font-display text-4xl font-black text-accent-ink">{player.result}</p>
        <p className="mt-2 flex items-center justify-center gap-1.5 text-sm font-semibold text-leaf">
          <CheckIcon className="size-4" /> Result recorded
        </p>
      </div>
    );
  }

  if (isMe) {
    return (
      <button
        type="button"
        disabled={!canTrigger || rolling}
        onClick={onTrigger}
        className="flex min-h-20 items-center justify-center gap-2 rounded-lg border-2 border-ink bg-accent px-4 text-lg font-black uppercase leading-tight tracking-wide text-on-accent shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:text-white disabled:shadow-none"
      >
        {rolling ? (
          <>
            <DiceIcon className="size-6 motion-safe:animate-roll" />
            Randomizing...
          </>
        ) : (
          <>
            <DiceIcon className="size-6" />
            Let randomness decide
          </>
        )}
      </button>
    );
  }

  return (
    <p className="rounded-lg border-2 border-dashed border-muted px-3 py-5 text-center text-sm font-semibold text-muted">
      Waiting for {player.name}
    </p>
  );
}
