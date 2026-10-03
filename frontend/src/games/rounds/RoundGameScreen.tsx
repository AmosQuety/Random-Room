import { unjoinedNames } from "../../lib/players";
import type { ReactNode } from "react";
import { CheckIcon, FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import type { GameScreenProps } from "../types";
import { Countdown } from "./Countdown";
import { GameOver, HostBar, ProgressChips } from "./parts";
import type { RoundPayload } from "./types";
import { useCountdown } from "./useCountdown";

export interface InputContext<TPrompt> {
  prompt: TPrompt;
  me: string;
  players: string[];
  disabled: boolean;
  submit: (answer: unknown) => void;
}

interface Props<TPrompt, TResult, TAnswer> extends GameScreenProps<RoundPayload<TPrompt, TResult, TAnswer>> {
  /** What one point is called on the scoreboard, e.g. "agreed". */
  scoreUnit: string;
  /** One line under the round number, e.g. "Would you rather..." */
  lead?: string;
  renderPrompt: (prompt: TPrompt) => ReactNode;
  renderInput: (context: InputContext<TPrompt>) => ReactNode;
  /** Shown once a player has answered, in place of the input. */
  renderMyAnswer: (answer: TAnswer, prompt: TPrompt) => ReactNode;
  renderResult: (result: TResult, prompt: TPrompt, me: string) => ReactNode;
}

/**
 * The screen for every prompt-and-answer game: lobby, the prompt with a countdown and private answering,
 * the shared reveal, host controls, and the scoreboard. A game supplies only how its prompt, input and result look.
 */
export function RoundGameScreen<TPrompt, TResult, TAnswer>({
  me,
  isHost,
  players,
  session,
  payload,
  busy,
  onAction,
  scoreUnit,
  lead,
  renderPrompt,
  renderInput,
  renderMyAnswer,
  renderResult,
}: Props<TPrompt, TResult, TAnswer>) {
  const live = session.status === "Active";
  const collecting = live && payload.phase === "collecting";
  const revealed = payload.phase === "revealed" || payload.phase === "complete";
  const over = session.status === "Completed" || payload.phase === "complete";
  const secondsLeft = useCountdown(payload.timer, collecting, () => onAction("tick"));
  const lastRound = payload.round >= payload.totalRounds;
  const iAnswered = payload.answered[me] ?? false;
  const names = players.map((p) => p.name);

  return (
    <>
      <p role="status" className="sr-only">
        {over ? "Game over" : revealed ? `Round ${payload.round} results are in` : collecting ? `Round ${payload.round} of ${payload.totalRounds}` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">{payload.totalRounds} rounds</p>
          <p className="mt-1 text-muted">
            {isHost ? "Press Start round below when everyone is here." : "Waiting for the host to start."}
            {payload.timeLimitSeconds ? ` ${payload.timeLimitSeconds} seconds per round.` : ""}
          </p>
        </section>
      )}

      {payload.prompt !== null && !over && (
        <section aria-labelledby="round-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p id="round-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
              Round {payload.round} of {payload.totalRounds}
            </p>
            {secondsLeft !== null && payload.timeLimitSeconds !== null && (
              <Countdown secondsLeft={secondsLeft} totalSeconds={payload.timeLimitSeconds} />
            )}
          </div>

          {lead && <p className="-mb-3 font-mono text-sm font-bold uppercase tracking-widest text-accent-ink">{lead}</p>}
          <div>{renderPrompt(payload.prompt)}</div>

          {collecting &&
            (payload.myAnswer !== null ? (
              <div className="rounded-lg border-2 border-ink bg-leaf-soft px-4 py-3">
                <p className="flex items-center gap-2 font-bold">
                  <CheckIcon className="size-4" /> Locked in
                </p>
                <div className="mt-1">{renderMyAnswer(payload.myAnswer, payload.prompt)}</div>
              </div>
            ) : (
              renderInput({
                prompt: payload.prompt,
                me,
                players: names,
                disabled: busy || iAnswered,
                submit: (answer) => onAction("answer", answer),
              })
            ))}

          {collecting && <ProgressChips done={payload.answered} notJoined={unjoinedNames(players)} />}

          {revealed && payload.result !== null && (
            <div className="motion-safe:animate-stamp">{renderResult(payload.result, payload.prompt, me)}</div>
          )}

          {isHost && live && (
            <HostBar>
              {collecting && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("reveal")}>
                  <PlayIcon className="size-4" /> Reveal now
                </Button>
              )}
              {payload.phase === "revealed" && (
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

      {over && revealed && payload.prompt !== null && payload.result !== null && (
        <section aria-label="Last round" className="rounded-xl border-2 border-ink bg-card p-5">
          <div className="mb-3">{renderPrompt(payload.prompt)}</div>
          {renderResult(payload.result, payload.prompt, me)}
        </section>
      )}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit={scoreUnit} />
    </>
  );
}
