import { useState } from "react";
import { CheckIcon, CloseIcon } from "../../components/icons";
import { fieldInputClass } from "../../components/styles";
import { Button } from "../../components/ui";
import { RoundGameScreen } from "../rounds/RoundGameScreen";
import type { RoundPayload } from "../rounds/types";
import type { GameScreenProps } from "../types";
import { isHttpsLink } from "./kit";

interface PromptView {
  clue: string;
  link: string | null;
}

interface Result {
  clue: string;
  accepted: string[];
  first: string | null;
  results: { player: string; text: string; correct: boolean }[];
}

interface Answer {
  text: string;
}

export type NameThatPayload = RoundPayload<PromptView, Result, Answer>;

function Clue({ prompt }: { prompt: PromptView }) {
  return (
    <>
      <h2 className="font-display text-2xl font-black leading-tight sm:text-3xl">{prompt.clue}</h2>
      {prompt.link && isHttpsLink(prompt.link) && (
        // Opened in a new tab and never embedded, so the host's link cannot run anything inside this page.
        <a
          href={prompt.link}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-flex min-h-11 items-center rounded-lg border-2 border-ink bg-accent-soft px-4 font-bold underline"
        >
          Open the clip (new tab)
        </a>
      )}
    </>
  );
}

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
      <label htmlFor="name-that-guess" className="sr-only">
        Your guess
      </label>
      <input
        id="name-that-guess"
        value={text}
        maxLength={80}
        autoComplete="off"
        placeholder="Name that song or movie"
        onChange={(e) => setText(e.target.value)}
        className={fieldInputClass}
      />
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Lock it in
      </Button>
    </form>
  );
}

export function NameThatGameScreen(props: GameScreenProps<NameThatPayload>) {
  return (
    <RoundGameScreen<PromptView, Result, Answer>
      {...props}
      lead="Name that..."
      scoreUnit="points"
      renderPrompt={(prompt) => <Clue prompt={prompt} />}
      renderInput={({ disabled, submit }) => <GuessForm disabled={disabled} submit={submit} />}
      renderMyAnswer={(answer) => <p>Your guess: {answer.text}</p>}
      renderResult={(result, _prompt, me) => (
        <div className="flex flex-col gap-3">
          <p className="font-display text-xl font-black">
            Answer: <span className="text-accent-ink">{result.accepted[0]}</span>
          </p>
          {result.accepted.length > 1 && <p className="text-sm text-muted">Also accepted: {result.accepted.slice(1).join(", ")}</p>}
          <ul className="flex flex-col gap-2">
            {result.results.map((r) => (
              <li key={r.player} className={`flex items-center gap-2 rounded-lg border-2 border-ink bg-card px-3 py-2 ${r.player === me ? "ring-4 ring-mustard/60" : ""}`}>
                {r.correct ? <CheckIcon className="size-4 text-leaf" /> : <CloseIcon className="size-4 text-tomato" />}
                <span className="font-bold">{r.player}</span>
                <span className="min-w-0 flex-1 truncate text-muted">{r.text}</span>
                <span className="font-mono text-xs uppercase tracking-widest">
                  {r.correct ? (r.player === result.first ? "Correct, first (+2)" : "Correct (+1)") : "Wrong"}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    />
  );
}
