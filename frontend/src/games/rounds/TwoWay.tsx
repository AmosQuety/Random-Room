import type { PromptEditorProps } from "./RoundSetupForm";
import { fieldInputClass } from "../../components/styles";
import type { GameScreenProps } from "../types";
import { ResultBar } from "./ResultBar";
import { RoundGameScreen } from "./RoundGameScreen";
import { choiceBadgeClass, choiceButtonClass } from "./styles";
import type { TwoWayAnswer, TwoWayInput, TwoWayPayload, TwoWayPromptView, TwoWayResult } from "./twoWay";

export function TwoWayEditor({ value, onChange, index, invalid }: PromptEditorProps<TwoWayInput>) {
  return (
    <div className="grid gap-2 sm:grid-cols-2">
      <input
        value={value.a}
        maxLength={100}
        aria-label={`Option A for prompt ${index + 1}`}
        aria-invalid={invalid && value.a.trim() === ""}
        placeholder="Option A"
        onChange={(e) => onChange({ ...value, a: e.target.value })}
        className={fieldInputClass}
      />
      <input
        value={value.b}
        maxLength={100}
        aria-label={`Option B for prompt ${index + 1}`}
        aria-invalid={invalid && value.b.trim() === ""}
        placeholder="Option B"
        onChange={(e) => onChange({ ...value, b: e.target.value })}
        className={fieldInputClass}
      />
    </div>
  );
}

const LETTERS = ["A", "B"] as const;

interface Props extends GameScreenProps<TwoWayPayload> {
  lead: string;
}

export function TwoWayGameScreen({ lead, ...screen }: Props) {
  return (
    <RoundGameScreen<TwoWayPromptView, TwoWayResult, TwoWayAnswer>
      {...screen}
      lead={lead}
      scoreUnit="with the majority"
      renderPrompt={() => null}
      renderInput={({ prompt, disabled, submit }) => (
        <div className="grid gap-3 sm:grid-cols-2">
          {prompt.options.map((option, i) => (
            <button key={i} type="button" disabled={disabled} onClick={() => submit({ choice: i })} className={choiceButtonClass}>
              <span className={choiceBadgeClass}>{LETTERS[i]}</span>
              {option}
            </button>
          ))}
        </div>
      )}
      renderMyAnswer={(answer, prompt) => <p>You picked: {prompt.options[answer.choice]}</p>}
      renderResult={(result, _prompt, me) => (
        <ol className="flex flex-col gap-3">
          {result.options.map((option, i) => (
            <ResultBar
              key={i}
              label={option}
              count={result.counts[i]}
              total={result.counts[0] + result.counts[1]}
              unit="vote"
              highlight={result.majority === i ? "Majority" : undefined}
              people={result.voters[i]}
              mine={result.voters[i].includes(me)}
            />
          ))}
        </ol>
      )}
    />
  );
}
