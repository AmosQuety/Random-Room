import { useState } from "react";
import { FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { fieldInputClass } from "../../components/styles";
import { Button, Eyebrow } from "../../components/ui";
import { Countdown } from "../rounds/Countdown";
import { GameOver, HostBar, NotJoinedNote } from "../rounds/parts";
import { useCountdown } from "../rounds/useCountdown";
import type { GameScreenProps } from "../types";
import { OUTCOME_TEXT, type ForbiddenPayload } from "./types";

function CardBox({ payload }: { payload: ForbiddenPayload }) {
  if (!payload.card) return null;
  const describing = payload.role === "describer";
  return (
    <div className="rounded-lg border-2 border-ink bg-accent-soft p-4">
      <p className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
        {describing ? "Your card. Only you and the judge can see it" : "The card. Only you and the describer can see it"}
      </p>
      <p className="mt-1 font-display text-3xl font-black">{payload.card.word}</p>
      <p className="mt-2 text-sm font-bold">Forbidden words:</p>
      <ul className="mt-1 flex flex-wrap gap-2">
        {payload.card.forbidden.map((word) => (
          <li key={word} className="rounded-full border-2 border-ink bg-card px-3 py-1 text-sm font-semibold">
            {word}
          </li>
        ))}
      </ul>
    </div>
  );
}

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
      <label htmlFor="taboo-guess" className="font-bold">
        Your guess
      </label>
      <div className="flex flex-col gap-3 sm:flex-row">
        <input id="taboo-guess" value={text} maxLength={40} autoComplete="off" onChange={(e) => setText(e.target.value)} className={fieldInputClass} />
        <Button type="submit" variant="accent" size="lg" disabled={!clean || disabled || left === 0}>
          Guess
        </Button>
      </div>
      <p className="text-sm text-muted">{left} guesses left this round</p>
    </form>
  );
}

export function ForbiddenGameScreen({ me, isHost, players, session, payload, busy, onAction }: GameScreenProps<ForbiddenPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const playing = live && payload.phase === "playing";
  const secondsLeft = useCountdown(payload.timer, playing, () => onAction("tick"));
  const lastRound = payload.round >= payload.totalRounds;
  const canFlag = playing && (payload.role === "judge" || isHost) && payload.role !== "describer";

  return (
    <>
      <p role="status" aria-live="polite" className="sr-only">
        {over ? "Game over" : payload.phase === "playing" ? `Round ${payload.round}. ${payload.describer} is describing.` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Say it without saying it</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start game below when everyone is here (3 or more players)." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.describer !== null && !over && (
        <section aria-labelledby="forbidden-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p id="forbidden-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
              Round {payload.round} of {payload.totalRounds}
            </p>
            {playing && secondsLeft !== null && payload.timeLimitSeconds !== null && <Countdown secondsLeft={secondsLeft} totalSeconds={payload.timeLimitSeconds} />}
          </div>

          <p className="font-display text-2xl font-black sm:text-3xl">
            {payload.role === "describer" ? "You are describing" : `${payload.describer} is describing`}
          </p>
          <NotJoinedNote name={payload.describer} players={players} />
          <p className="text-sm text-muted">
            {payload.role === "describer" && `${payload.judge} is the judge and can see your card. Do not say the word or any forbidden word.`}
            {payload.role === "judge" && `You are the judge. Flag ${payload.describer} if they say the word or a forbidden word.`}
            {payload.role === "guesser" && `${payload.judge} is the judge. Listen, then type your guess.`}
          </p>

          {payload.phase === "playing" && <CardBox payload={payload} />}

          {playing && payload.role === "guesser" && <GuessForm disabled={busy} left={payload.myGuessesLeft} onGuess={(text) => onAction("guess", { text })} />}

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
                {payload.result.outcome === "guessed" ? `${payload.result.guesser} guessed it.` : `${payload.describer} ${OUTCOME_TEXT[payload.result.outcome]}.`}
              </p>
              <p className="mt-1 text-sm text-muted">Forbidden: {payload.result.forbidden.join(", ")}</p>
            </div>
          )}

          {(payload.role === "describer" || canFlag || (isHost && live)) && live && (
            <HostBar>
              {playing && payload.role === "describer" && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("skip")}>
                  Skip this card
                </Button>
              )}
              {canFlag && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("flag")}>
                  <FlagIcon className="size-4" /> Flag a slip
                </Button>
              )}
              {isHost && playing && (
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
