import { useState } from "react";
import { fieldInputClass } from "../../components/styles";
import { Button } from "../../components/ui";
import { ResultBar } from "../rounds/ResultBar";
import { RoundGameScreen } from "../rounds/RoundGameScreen";
import type { RoundPayload } from "../rounds/types";
import type { GameScreenProps } from "../types";

interface PromptView {
  text: string;
  boardSize: number;
}

interface Result {
  text: string;
  board: { text: string; points: number; guessedBy: string[] }[];
  guesses: { player: string; text: string; matched: boolean }[];
}

interface Answer {
  text: string;
}

export type SurveyShowdownPayload = RoundPayload<PromptView, Result, Answer>;

function GuessForm({ disabled, submit }: { disabled: boolean; submit: (answer: unknown) => void }) {
  const [text, setText] = useState("");
  const ready = text.trim().length > 0;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && !disabled) submit({ text: text.trim() });
      }}
      className="flex flex-col gap-3 sm:flex-row"
    >
      <label htmlFor="survey-guess" className="sr-only">
        Your guess
      </label>
      <input
        id="survey-guess"
        value={text}
        maxLength={60}
        autoComplete="off"
        placeholder="Type your best guess"
        onChange={(e) => setText(e.target.value)}
        className={fieldInputClass}
      />
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Lock it in
      </Button>
    </form>
  );
}

export function SurveyShowdownGameScreen(props: GameScreenProps<SurveyShowdownPayload>) {
  return (
    <RoundGameScreen<PromptView, Result, Answer>
      {...props}
      lead="Survey says..."
      scoreUnit="points"
      renderPrompt={(prompt) => (
        <>
          <h3 className="font-display text-2xl font-black leading-tight sm:text-3xl">{prompt.text}</h3>
          <p className="mt-1 text-sm text-muted">{prompt.boardSize} answers are on the board.</p>
        </>
      )}
      renderInput={({ disabled, submit }) => <GuessForm disabled={disabled} submit={submit} />}
      renderMyAnswer={(answer) => <p>Your guess: {answer.text}</p>}
      renderResult={(result, _prompt, me) => {
        const top = Math.max(...result.board.map((b) => b.points));
        return (
          <div className="flex flex-col gap-4">
            <ol className="flex flex-col gap-3">
              {result.board.map((row) => (
                <ResultBar
                  key={row.text}
                  label={row.text}
                  count={row.points}
                  total={top}
                  unit="point"
                  measure="relative"
                  highlight={row.points === top ? "Top answer" : undefined}
                  people={row.guessedBy}
                  mine={row.guessedBy.includes(me)}
                />
              ))}
            </ol>
            <ul className="text-sm text-muted">
              {result.guesses
                .filter((g) => !g.matched)
                .map((g) => (
                  <li key={g.player}>
                    {g.player} guessed &quot;{g.text}&quot; - not on the board.
                  </li>
                ))}
            </ul>
          </div>
        );
      }}
    />
  );
}
