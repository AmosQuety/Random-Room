import { CheckIcon, FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar, ProgressChips } from "../rounds/parts";
import { choiceBadgeClass, choiceButtonClass } from "../rounds/styles";
import type { GameScreenProps } from "../types";
import { StatementForm } from "./StatementForm";
import type { TwoTruthsPayload, TwoTruthsResult } from "./types";

function Reveal({ result, me }: { result: TwoTruthsResult; me: string }) {
  return (
    <div className="flex flex-col gap-3">
      <ol className="flex flex-col gap-3">
        {result.statements.map((text, i) => {
          const isLie = i === result.lie;
          const voters = result.votes[i];
          return (
            <li key={i} className={`rounded-lg border-2 border-ink p-3 ${isLie ? "bg-tomato-soft" : "bg-card"}`}>
              <p className="flex items-start justify-between gap-3 font-display text-lg font-bold">
                <span>{text}</span>
                <span className="shrink-0 rounded-md border-2 border-ink bg-card px-2 py-0.5 font-mono text-xs uppercase tracking-widest">
                  {isLie ? "The lie" : "True"}
                </span>
              </p>
              <p className="mt-1 text-sm text-muted">
                {voters.length === 0 ? "Nobody voted for this one." : `Voted lie: ${voters.join(", ")}`}
                {voters.includes(me) && " (you)"}
              </p>
            </li>
          );
        })}
      </ol>
      <p className="text-sm">
        {result.fooled.length === 0 ? "Nobody was fooled." : `Fooled: ${result.fooled.join(", ")}.`}{" "}
        {result.caught.length === 0 ? "" : `Caught it: ${result.caught.join(", ")}.`}
      </p>
    </div>
  );
}

export function TwoTruthsGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<TwoTruthsPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const isStoryteller = payload.storyteller === me;
  const lastRound = payload.round >= payload.totalRounds;

  return (
    <>
      <p role="status" className="sr-only">
        {over ? "Game over" : payload.storyteller ? `Round ${payload.round} of ${payload.totalRounds}. ${payload.storyteller} is the storyteller.` : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">{payload.totalRounds} storytellers</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start game below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.storyteller && !over && (
        <section aria-labelledby="turn-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <div>
            <p className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
              Round {payload.round} of {payload.totalRounds}
            </p>
            <h3 id="turn-heading" className="mt-1 font-display text-2xl font-black sm:text-3xl">
              {isStoryteller ? "Your turn to fool everyone" : `${payload.storyteller}'s turn`}
            </h3>
          </div>

          {payload.phase === "submitting" &&
            (isStoryteller ? (
              <StatementForm disabled={busy || !live} onSubmit={(body) => onAction("submit", body)} />
            ) : (
              <p className="rounded-lg border-2 border-dashed border-ink px-4 py-3">
                Waiting for {payload.storyteller} to write two truths and a lie.
              </p>
            ))}

          {payload.phase === "voting" && payload.statements && (
            <>
              {isStoryteller ? (
                <div className="flex flex-col gap-3">
                  <p className="text-sm text-muted">Your statements. Keep a straight face.</p>
                  <ol className="flex flex-col gap-2">
                    {payload.statements.map((text, i) => (
                      <li key={i} className="flex items-start justify-between gap-3 rounded-lg border-2 border-ink bg-paper p-3 font-semibold">
                        <span>{text}</span>
                        {payload.myLie === i && <span className="shrink-0 font-mono text-xs uppercase tracking-widest text-tomato">Your lie</span>}
                      </li>
                    ))}
                  </ol>
                </div>
              ) : (
                <div className="flex flex-col gap-3">
                  <p className="text-sm text-muted">Which one is the lie?</p>
                  <div className="flex flex-col gap-3">
                    {payload.statements.map((text, i) => (
                      <button
                        key={i}
                        type="button"
                        disabled={busy || !live || payload.myVote !== null}
                        aria-pressed={payload.myVote === i}
                        onClick={() => onAction("vote", { choice: i })}
                        className={`${choiceButtonClass} ${payload.myVote === i ? "!bg-accent-soft ring-4 ring-mustard/60" : ""}`}
                      >
                        <span className={choiceBadgeClass}>{i + 1}</span>
                        {text}
                      </button>
                    ))}
                  </div>
                  {payload.myVote !== null && (
                    <p className="flex items-center gap-2 font-bold">
                      <CheckIcon className="size-4" /> Vote locked in.
                    </p>
                  )}
                </div>
              )}
              <ProgressChips done={payload.voted} verb="voted" />
            </>
          )}

          {payload.result && payload.phase === "revealed" && (
            <div className="motion-safe:animate-stamp">
              <Reveal result={payload.result} me={me} />
            </div>
          )}

          {isHost && live && (
            <HostBar>
              {payload.phase === "submitting" && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("skip")}>
                  Skip {payload.storyteller}
                </Button>
              )}
              {payload.phase === "voting" && (
                <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("reveal")}>
                  <PlayIcon className="size-4" /> Reveal now
                </Button>
              )}
              {payload.phase === "revealed" && (
                <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("next")}>
                  {lastRound ? <FlagIcon className="size-4" /> : <PlayIcon className="size-4" />}
                  {lastRound ? "Finish game" : "Next storyteller"}
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
