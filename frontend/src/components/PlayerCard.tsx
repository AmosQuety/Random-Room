import type { PlayerView } from "../lib/types";
import { Avatar } from "./Avatar";

interface Props {
  player: PlayerView;
  isMe: boolean;
  canTrigger: boolean;
  rolling: boolean;
  onTrigger: () => void;
}

export function PlayerCard({ player, isMe, canTrigger, rolling, onTrigger }: Props) {
  return (
    <li
      className={`flex flex-col gap-4 rounded-xl border-2 border-ink p-4 shadow-ticket ${isMe ? "bg-card ring-4 ring-mustard/60" : "bg-card"}`}
    >
      <div className="flex items-center gap-3">
        <Avatar name={player.name} />
        <div className="min-w-0">
          <p className="truncate font-display text-xl font-bold">
            {player.name}
            {isMe && <span className="ml-2 font-mono text-xs font-normal uppercase text-muted">(you)</span>}
          </p>
          <p className="text-sm text-muted">{player.online ? "🟢 Online" : "⚪ Offline"}</p>
        </div>
      </div>

      <CardBody player={player} canTrigger={canTrigger} rolling={rolling} isMe={isMe} onTrigger={onTrigger} />
    </li>
  );
}

function CardBody({ player, canTrigger, rolling, isMe, onTrigger }: Omit<Props, "player"> & { player: PlayerView }) {
  if (player.result) {
    return (
      <div key={player.result} className="animate-stamp rounded-lg border-2 border-ink bg-paper px-3 py-4 text-center">
        <p className="font-mono text-xs font-bold uppercase tracking-widest text-muted">🎲 System random result</p>
        <p className="mt-1 font-display text-4xl font-black text-tomato">{player.result}</p>
        <p className="mt-2 text-sm font-semibold text-leaf">✅ Result recorded</p>
      </div>
    );
  }

  if (isMe) {
    return (
      <button
        type="button"
        disabled={!canTrigger || rolling}
        onClick={onTrigger}
        className="min-h-20 rounded-lg border-2 border-ink bg-tomato px-4 text-lg font-black uppercase leading-tight tracking-wide text-white shadow-ticket-sm transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:bg-muted disabled:shadow-none"
      >
        {rolling ? (
          <>
            <span className="animate-roll mr-2 inline-block">🎲</span>Randomizing...
          </>
        ) : (
          "🎲 Let randomness decide"
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
