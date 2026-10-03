import { useState } from "react";
import { fieldInputClass } from "../../components/styles";
import { Button } from "../../components/ui";
import { RoundGameScreen } from "../rounds/RoundGameScreen";
import type { RoundPayload } from "../rounds/types";
import type { GameScreenProps } from "../types";
import { MAX_WORD } from "./kit";

interface PromptView {
  labels: string[];
  /** Which blanks each player is asked for (indexes into labels). */
  assigned: Record<string, number[]>;
}

type Part = { text: string } | { word: string; by: string | null; label: string };

interface Result {
  title: string;
  parts: Part[];
}

interface Answer {
  words: string[];
}

export type MadLibsPayload = RoundPayload<PromptView, Result, Answer>;

function WordsForm({ prompt, me, disabled, submit }: { prompt: PromptView; me: string; disabled: boolean; submit: (answer: unknown) => void }) {
  const mine = prompt.assigned[me] ?? [];
  const [words, setWords] = useState<string[]>(() => mine.map(() => ""));
  const ready = words.length === mine.length && words.every((w) => w.trim() !== "");

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && !disabled) submit({ words: words.map((w) => w.trim()) });
      }}
      className="flex flex-col gap-3"
    >
      <p className="text-sm text-muted">You cannot see the story. Give a word for each gap you are asked for.</p>
      {mine.map((blank, i) => (
        <div key={blank}>
          <label htmlFor={`word-${blank}`} className="mb-1 block font-mono text-xs font-bold uppercase tracking-widest">
            {prompt.labels[blank]}
          </label>
          <input
            id={`word-${blank}`}
            value={words[i] ?? ""}
            maxLength={MAX_WORD}
            autoComplete="off"
            onChange={(e) => setWords(words.map((w, j) => (j === i ? e.target.value : w)))}
            className={fieldInputClass}
          />
        </div>
      ))}
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Lock it in
      </Button>
    </form>
  );
}

export function MadLibsGameScreen(props: GameScreenProps<MadLibsPayload>) {
  return (
    <RoundGameScreen<PromptView, Result, Answer>
      {...props}
      lead="Fill in the blanks"
      scoreUnit="points"
      renderPrompt={(prompt) => (
        <div>
          <h2 className="font-display text-2xl font-black leading-tight sm:text-3xl">A story is waiting</h2>
          <p className="mt-1 text-muted">{prompt.labels.length} gaps to fill, shared between the players.</p>
        </div>
      )}
      renderInput={({ prompt, me, disabled, submit }) => <WordsForm prompt={prompt} me={me} disabled={disabled} submit={submit} />}
      renderMyAnswer={(answer) => <p>Your words: {answer.words.join(", ")}</p>}
      renderResult={(result) => (
        <div className="flex flex-col gap-3">
          <p className="font-display text-xl font-black">{result.title}</p>
          <p className="text-lg leading-relaxed">
            {result.parts.map((part, i) =>
              "word" in part ? (
                <mark key={i} title={part.by ? `${part.label}, from ${part.by}` : part.label} className="rounded bg-accent-soft px-1 font-bold text-ink">
                  {part.word}
                </mark>
              ) : (
                <span key={i}>{part.text}</span>
              ),
            )}
          </p>
        </div>
      )}
    />
  );
}
