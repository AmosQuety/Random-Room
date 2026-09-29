import { Avatar } from "../../components/Avatar";
import { CheckIcon, FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar, ProgressChips } from "../rounds/parts";
import { choiceButtonClass } from "../rounds/styles";
import type { GameScreenProps } from "../types";
import { FactForm } from "./FactForm";
import type { GuessWhoPayload, GuessWhoResult } from "./types";

function Reveal({ result, me }: { result: GuessWhoResult; me: string }) {
  return (
    <div className="flex flex-col gap-3">
      <p className="font-display text-xl font-black">
        Written by <span className="text-accent-ink">{result.author}</span>
      </p>
      <p className="text-sm">
        {result.correct.length === 0 ? "Nobody guessed it." : `Guessed it: ${result.correct.join(", ")}.`}
      </p>
      <ul className="flex flex-col gap-1 text-sm text-muted">
        {result.guesses.map((g) => (
          <li key={g.player}>
            {g.player}
            {g.player === me ? " (you)" : ""} guessed {g.guessed}
            {g.guessed === result.author ? " - right" : ""}
          </li>
        ))}
      </ul>
    </div>
  );
}

export function GuessWhoGameScreen({ me, isHost, players, session, payload, busy, onAction }: GameScreenProps<GuessWhoPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const submittedCount = Object.values(payload.submitted).filter(Boolean).length;
  const lastRound = payload.round >= payload.totalRounds;

  return (
    <>
      <p role="status" className="sr-only">
        {over ? "Game over" : payload.phase === "voting" ? `Fact ${payload.round} of ${payload.totalRounds}` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Everyone writes one fact</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start round below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.phase === "submitting" && live && (
        <section aria-labelledby="facts-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <h3 id="facts-heading" className="font-display text-2xl font-black sm:text-3xl">
            Write your fact
          </h3>
          {payload.myFact === null ? (
            <FactForm disabled={busy} onSubmit={(text) => onAction("submit", { text })} />
          ) : (
            <div className="rounded-lg border-2 border-ink bg-leaf-soft px-4 py-3">
              <p className="flex items-center gap-2 font-bold">
                <CheckIcon className="size-4" /> Your fact is in
              </p>
              <p className="mt-1">{payload.myFact}</p>
            </div>
          )}
          <ProgressChips done={payload.submitted} verb="submitted" />
          {isHost && (
            <HostBar>
              <Button variant="secondary" size="sm" disabled={busy || submittedCount < 2} onClick={() => onAction("begin")}>
                <PlayIcon className="size-4" /> Begin with {submittedCount} {submittedCount === 1 ? "fact" : "facts"}
              </Button>
              {submittedCount < 2 && <span className="text-sm text-muted">Needs at least 2 facts.</span>}
            </HostBar>
          )}
        </section>
      )}

      {(payload.phase === "voting" || payload.phase === "revealed") && payload.text !== null && !over && (
        <section aria-labelledby="fact-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <p id="fact-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
            Fact {payload.round} of {payload.totalRounds}
          </p>
          <blockquote className="font-display text-2xl font-black leading-tight sm:text-3xl">&ldquo;{payload.text}&rdquo;</blockquote>

          {payload.phase === "voting" &&
            (payload.isMine ? (
              <p className="rounded-lg border-2 border-dashed border-ink px-4 py-3">This one is yours. Keep a straight face.</p>
            ) : payload.myGuess !== null ? (
              <p className="flex items-center gap-2 rounded-lg border-2 border-ink bg-leaf-soft px-4 py-3 font-bold">
                <CheckIcon className="size-4" /> You guessed {payload.myGuess}
              </p>
            ) : (
              <div>
                <p className="mb-2 text-sm text-muted">Who wrote it?</p>
                <div className="grid gap-3 sm:grid-cols-2">
                  {players
                    .filter((p) => p.name !== me)
                    .map((p) => (
                      <button key={p.name} type="button" disabled={busy || !live} onClick={() => onAction("guess", { player: p.name })} className={choiceButtonClass}>
                        <Avatar name={p.name} />
                        {p.name}
                      </button>
                    ))}
                </div>
              </div>
            ))}

          {payload.phase === "voting" && (
            <p aria-live="polite" className="text-sm text-muted">
              {payload.guessCount} of {payload.guessesNeeded} have guessed
            </p>
          )}

          {payload.phase === "revealed" && payload.result && (
            <div className="motion-safe:animate-stamp">
              <Reveal result={payload.result} me={me} />
            </div>
          )}

          {isHost && live && (
            <HostBar>
              {payload.phase === "voting" && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("reveal")}>
                  <PlayIcon className="size-4" /> Reveal now
                </Button>
              )}
              {payload.phase === "revealed" && (
                <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("next")}>
                  {lastRound ? <FlagIcon className="size-4" /> : <PlayIcon className="size-4" />}
                  {lastRound ? "Finish game" : "Next fact"}
                </Button>
              )}
            </HostBar>
          )}
        </section>
      )}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="correct guesses" />
    </>
  );
}
