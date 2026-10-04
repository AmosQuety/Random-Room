import { FlagIcon, PlayIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar, HostScoreNotes } from "../rounds/parts";
import type { GameScreenProps } from "../types";
import type { BuzzerPayload } from "./types";

const buzzClass =
  "grid min-h-40 w-full place-items-center rounded-full border-4 border-ink bg-accent px-6 py-8 font-display text-4xl font-black text-on-accent shadow-ticket transition active:translate-x-0.5 active:translate-y-0.5 active:shadow-none disabled:cursor-not-allowed disabled:bg-paper disabled:text-muted disabled:shadow-none sm:min-h-52 sm:text-5xl";

function status(payload: BuzzerPayload, me: string, isHost: boolean): string {
  if (payload.phase === "waiting") return isHost ? "Read the prompt, then open the buzzers." : "Get ready. The host will open the buzzers.";
  if (payload.phase === "open" && payload.buzzed) return payload.buzzed === me ? "You buzzed first. Answer out loud." : `${payload.buzzed} buzzed first.`;
  if (payload.phase === "open") return payload.lockedOut.includes(me) ? "You missed this one. Wait for the next round." : "Buzzers open. Go!";
  if (payload.phase === "resolved") return payload.winner ? `${payload.winner} got it.` : "Nobody got that one.";
  return "";
}

function Round({ payload, me, isHost, live, busy, onAction }: { payload: BuzzerPayload; me: string; isHost: boolean; live: boolean; busy: boolean; onAction: GameScreenProps<BuzzerPayload>["onAction"] }) {
  const lastRound = payload.round >= payload.totalRounds;
  const canBuzz = live && !isHost && payload.phase === "open" && payload.buzzed === null && !payload.lockedOut.includes(me);
  const message = status(payload, me, isHost);

  return (
    <section aria-labelledby="prompt-heading" className="flex flex-col gap-5 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
      <p id="prompt-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
        Round {payload.round} of {payload.totalRounds}
      </p>
      <p className="font-display text-2xl font-black leading-tight sm:text-3xl">{payload.prompt}</p>

      {!isHost && (
        <button type="button" disabled={!canBuzz || busy} onClick={() => onAction("buzz")} className={buzzClass}>
          {payload.phase === "open" && payload.buzzed === null && canBuzz ? "BUZZ" : payload.buzzed === me ? "You're up" : "Buzz"}
        </button>
      )}

      <p role="status" aria-live="assertive" className="rounded-lg border-2 border-ink bg-accent-soft px-4 py-3 font-bold">
        {message}
      </p>

      {payload.lockedOut.length > 0 && <p className="text-sm text-muted">Missed this round: {payload.lockedOut.join(", ")}</p>}

      {isHost && live && (
        <HostBar>
          {payload.phase === "waiting" && (
            <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("open")}>
              <PlayIcon className="size-4" /> Open the buzzers
            </Button>
          )}
          {payload.phase === "open" && payload.buzzed && (
            <>
              <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("correct")}>
                Correct
              </Button>
              <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("wrong")}>
                Wrong
              </Button>
            </>
          )}
          {(payload.phase === "waiting" || payload.phase === "open") && (
            <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("skip")}>
              Skip round
            </Button>
          )}
          {payload.phase === "resolved" && (
            <Button variant="accent" size="sm" disabled={busy} onClick={() => onAction("next")}>
              {lastRound ? <FlagIcon className="size-4" /> : <PlayIcon className="size-4" />}
              {lastRound ? "Finish game" : "Next round"}
            </Button>
          )}
        </HostBar>
      )}
    </section>
  );
}

export function BuzzerGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<BuzzerPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";

  return (
    <>
      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Fingers on the buzzers</p>
          <p className="mt-1 text-muted">{isHost ? "You read the prompts and judge answers. Press Start game below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {payload.prompt !== null && !over && <Round payload={payload} me={me} isHost={isHost} live={live} busy={busy} onAction={onAction} />}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="points" />
      <HostScoreNotes notes={payload.hostScoring} />
    </>
  );
}
