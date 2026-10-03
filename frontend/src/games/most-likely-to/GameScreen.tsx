import { Avatar } from "../../components/Avatar";
import { ResultBar } from "../rounds/ResultBar";
import { RoundGameScreen } from "../rounds/RoundGameScreen";
import { StatementText } from "../rounds/Statement";
import type { StatementPromptView } from "../rounds/statement";
import { choiceButtonClass } from "../rounds/styles";
import type { RoundPayload } from "../rounds/types";
import type { GameScreenProps } from "../types";

interface Result {
  text: string;
  tally: { player: string; votes: number }[];
  winners: string[];
}

interface Answer {
  player: string;
}

export type MostLikelyToPayload = RoundPayload<StatementPromptView, Result, Answer>;

export function MostLikelyToGameScreen(props: GameScreenProps<MostLikelyToPayload>) {
  return (
    <RoundGameScreen<StatementPromptView, Result, Answer>
      {...props}
      scoreUnit="crowns"
      renderPrompt={(prompt) => <StatementText prompt={prompt} />}
      renderInput={({ me, players, disabled, submit }) => (
        <div>
          <p className="mb-2 text-sm text-muted">Vote for someone else. Nobody sees who voted for whom.</p>
          <div className="grid gap-3 sm:grid-cols-2">
            {players
              .filter((p) => p !== me)
              .map((p) => (
                <button key={p} type="button" disabled={disabled} onClick={() => submit({ player: p })} className={choiceButtonClass}>
                  <Avatar name={p} />
                  {p}
                </button>
              ))}
          </div>
        </div>
      )}
      renderMyAnswer={(answer) => <p>You voted for {answer.player}</p>}
      renderResult={(result) => {
        const total = result.tally.reduce((sum, t) => sum + t.votes, 0);
        return (
          <ol className="flex flex-col gap-3">
            {result.tally.map((t) => (
              <ResultBar key={t.player} label={t.player} count={t.votes} total={total} unit="vote" highlight={result.winners.includes(t.player) ? "Crowned" : undefined} />
            ))}
          </ol>
        );
      }}
    />
  );
}
