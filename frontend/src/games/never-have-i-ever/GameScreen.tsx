import { ResultBar } from "../rounds/ResultBar";
import { RoundGameScreen } from "../rounds/RoundGameScreen";
import { StatementText } from "../rounds/Statement";
import type { StatementPromptView } from "../rounds/statement";
import { choiceButtonClass } from "../rounds/styles";
import type { RoundPayload } from "../rounds/types";
import type { GameScreenProps } from "../types";

interface Result {
  text: string;
  have: string[];
  never: string[];
}

interface Answer {
  have: boolean;
}

export type NeverHaveIEverPayload = RoundPayload<StatementPromptView, Result, Answer>;

export function NeverHaveIEverGameScreen(props: GameScreenProps<NeverHaveIEverPayload>) {
  return (
    <RoundGameScreen<StatementPromptView, Result, Answer>
      {...props}
      lead="Never have I ever..."
      scoreUnit="still standing"
      renderPrompt={(prompt) => <StatementText prompt={prompt} />}
      renderInput={({ disabled, submit }) => (
        <div className="grid gap-3 sm:grid-cols-2">
          <button type="button" disabled={disabled} onClick={() => submit({ have: true })} className={choiceButtonClass}>
            I have
          </button>
          <button type="button" disabled={disabled} onClick={() => submit({ have: false })} className={choiceButtonClass}>
            Never
          </button>
        </div>
      )}
      renderMyAnswer={(answer) => <p>{answer.have ? "You have." : "Never for you."}</p>}
      renderResult={(result, _prompt, me) => {
        const total = result.have.length + result.never.length;
        return (
          <ol className="flex flex-col gap-3">
            <ResultBar label="I have" count={result.have.length} total={total} unit="player" people={result.have} mine={result.have.includes(me)} />
            <ResultBar label="Never" count={result.never.length} total={total} unit="player" people={result.never} mine={result.never.includes(me)} highlight="Still standing" />
          </ol>
        );
      }}
    />
  );
}
