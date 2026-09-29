import { useCallback, useState } from "react";
import { FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { fieldInputClass } from "../../components/styles";
import { Button, Eyebrow } from "../../components/ui";
import { Countdown } from "../rounds/Countdown";
import { GameOver, HostBar } from "../rounds/parts";
import { useCountdown } from "../rounds/useCountdown";
import type { GameScreenProps } from "../types";
import { Board } from "./Board";
import { OUTCOME_TEXT, type SketchPayload, type Stroke } from "./types";

function GuessForm({ disabled, left, onGuess }: { disabled: boolean; left: number; onGuess: (text: string) => void }) {
  const [text, setText] = useState("");
  const clean = text.trim();
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (clean && !disabled) {
          onGuess(clean);
          setText("");
        }
      }}
      className="flex flex-col gap-2"
    >
      <label htmlFor="sketch-guess" className="font-bold">
        Your guess
      </label>
      <div className="flex flex-col gap-3 sm:flex-row">
        <input id="sketch-guess" value={text} maxLength={40} autoComplete="off" onChange={(e) => setText(e.target.value)} className={fieldInputClass} />
        <Button type="submit" variant="accent" size="lg" disabled={!clean || disabled || left === 0}>
          Guess
        </Button>
      </div>
      <p className="text-sm text-muted">{left} guesses left this round</p>
    </form>
  );
}

export function SketchGuessGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<SketchPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const drawing = live && payload.phase === "drawing";
  const isDrawer = payload.drawer === me;
  const secondsLeft = useCountdown(payload.timer, drawing, () => onAction("tick"));
  const lastRound = payload.round >= payload.totalRounds;

  const sendStrokes = useCallback((strokes: Stroke[]) => onAction("strokes", { strokes }), [onAction]);
  const undo = useCallback(() => onAction("undo"), [onAction]);
  const clear = useCallback(() => onAction("clear"), [onAction]);

  return (
    <>
      <p role="status" aria-live="polite" className="sr-only">
        {over ? "Game over" : drawing ? `Round ${payload.round}. ${payload.drawer} is drawing.` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Draw it, guess it</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start round below when everyone is here (2 or more players)." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.drawer !== null && !over && (
        <section aria-labelledby="sketch-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p id="sketch-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
              Round {payload.round} of {payload.totalRounds}
            </p>
            {drawing && secondsLeft !== null && payload.timeLimitSeconds !== null && <Countdown secondsLeft={secondsLeft} totalSeconds={payload.timeLimitSeconds} />}
          </div>

          <p className="font-display text-2xl font-black sm:text-3xl">{isDrawer ? "You are drawing" : `${payload.drawer} is drawing`}</p>

          {isDrawer && payload.word && drawing && (
            <div className="rounded-lg border-2 border-ink bg-accent-soft p-4">
              <p className="font-mono text-xs font-bold uppercase tracking-widest text-muted">Your word. Only you can see it</p>
              <p className="mt-1 font-display text-3xl font-black">{payload.word}</p>
              <p className="mt-1 text-sm">Draw it. No letters or numbers.</p>
            </div>
          )}

          <Board
            strokes={payload.strokes}
            canDraw={drawing && isDrawer}
            label={isDrawer ? "Your drawing board" : `${payload.drawer}'s drawing`}
            onStrokes={sendStrokes}
            onUndo={undo}
            onClear={clear}
          />

          {drawing && !isDrawer && <GuessForm disabled={busy} left={payload.myGuessesLeft} onGuess={(text) => onAction("guess", { text })} />}

          {payload.guesses.length > 0 && (
            <div>
              <p className="mb-1 font-mono text-xs font-bold uppercase tracking-widest text-muted">Guesses</p>
              <ul aria-label="Guesses so far" className="flex flex-col gap-1 text-sm">
                {payload.guesses.slice(-8).map((g, i) => (
                  <li key={i}>
                    <strong>{g.player}:</strong> {g.text}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {payload.phase === "revealed" && payload.result && (
            <div className="rounded-lg border-2 border-ink bg-card p-4 motion-safe:animate-stamp">
              <p className="font-display text-xl font-black">
                The word was <span className="text-accent-ink">{payload.result.word}</span>
              </p>
              <p className="mt-1 text-sm">
                {payload.result.outcome === "guessed" ? `${payload.result.guesser} guessed it.` : `${payload.drawer} ${OUTCOME_TEXT[payload.result.outcome]}.`}
              </p>
            </div>
          )}

          {live && (isDrawer || isHost) && (
            <HostBar>
              {drawing && isDrawer && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("skip")}>
                  Skip this word
                </Button>
              )}
              {isHost && drawing && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("reveal")}>
                  Reveal now
                </Button>
              )}
              {isHost && payload.phase === "revealed" && (
                <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("next")}>
                  {lastRound ? <FlagIcon className="size-4" /> : <PlayIcon className="size-4" />}
                  {lastRound ? "Finish game" : "Next round"}
                </Button>
              )}
            </HostBar>
          )}
        </section>
      )}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="points" />
    </>
  );
}
