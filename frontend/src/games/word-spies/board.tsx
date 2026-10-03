import type { Owner, SpyCard } from "./types";

const OWNER_LABEL: Record<Owner, string> = { red: "Red", blue: "Blue", neutral: "Neutral", assassin: "Assassin" };

/** Colour never carries the meaning alone: every card that shows its owner also says it in words. */
const TONE: Record<Owner, string> = {
  red: "bg-tomato text-white",
  blue: "bg-sky text-white",
  neutral: "bg-paper text-ink",
  assassin: "bg-ink text-paper",
};

const HINT_RING: Record<Owner, string> = {
  red: "ring-4 ring-tomato",
  blue: "ring-4 ring-sky",
  neutral: "ring-2 ring-muted",
  assassin: "ring-4 ring-ink",
};

interface Props {
  board: SpyCard[];
  /** The key, when this viewer is a spymaster: unturned cards get a hint of their owner. */
  key_: Owner[] | null;
  canGuess: boolean;
  onGuess: (cell: number) => void;
}

export function Board({ board, key_, canGuess, onGuess }: Props) {
  return (
    <div role="group" aria-label="Word board" className="grid grid-cols-3 gap-2 min-[480px]:grid-cols-5 min-[480px]:gap-1 sm:gap-2">
      {board.map((card, i) => {
        const turned = card.owner !== null;
        const hint = !turned && key_ ? key_[i] : null;
        const label = card.owner ?? hint;
        return (
          <button
            key={i}
            type="button"
            disabled={!canGuess || turned}
            onClick={() => onGuess(i)}
            aria-label={`${card.word}${label ? ` ${OWNER_LABEL[label]}` : ""}${turned ? ", turned over" : hint ? ", secret" : ""}`}
            className={`grid min-h-14 place-items-center rounded-md border-2 border-ink min-w-0 p-1 text-center text-[0.8rem] font-bold leading-tight [overflow-wrap:break-word] min-[360px]:text-sm min-[480px]:text-[0.7rem] sm:text-sm transition disabled:cursor-default sm:min-h-20 ${
              turned ? TONE[card.owner!] : "bg-card hover:bg-accent-soft"
            } ${hint ? HINT_RING[hint] : ""}`}
          >
            <span>{card.word}</span>
            {label && <span className="font-mono text-[0.6rem] uppercase tracking-tighter opacity-90 min-[390px]:text-[0.7rem]">{OWNER_LABEL[label]}</span>}
          </button>
        );
      })}
    </div>
  );
}
