import { useState } from "react";
import { FlagIcon } from "../../components/icons";
import { Scoreboard } from "../../components/Scoreboard";
import { fieldInputClass } from "../../components/styles";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar } from "../rounds/parts";
import type { GameScreenProps } from "../types";
import type { ChainConfig, ChainPayload } from "./types";

interface Props extends GameScreenProps<ChainPayload> {
  config: ChainConfig;
}

function TurnForm({ config, prefix, disabled, onAdd }: { config: ChainConfig; prefix: string | null; disabled: boolean; onAdd: (text: string) => void }) {
  const [text, setText] = useState("");
  const clean = text.trim();
  const ready = clean.length > 0 && !(config.singleWord && /\s/.test(clean));
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && !disabled) {
          onAdd(clean);
          setText("");
        }
      }}
      className="flex flex-col gap-3"
    >
      <label htmlFor="chain-input" className="font-bold">
        {prefix ? <span className="text-accent-ink">{prefix} </span> : null}
        {config.inputHint}
      </label>
      <div className="flex flex-col gap-3 sm:flex-row">
        <input
          id="chain-input"
          value={text}
          maxLength={config.maxChars}
          autoComplete="off"
          aria-invalid={config.singleWord && /\s/.test(clean)}
          onChange={(e) => setText(e.target.value)}
          className={fieldInputClass}
        />
        <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
          Add it
        </Button>
      </div>
      {config.singleWord && /\s/.test(clean) && <p className="text-sm font-semibold text-tomato">Just one word per turn.</p>}
    </form>
  );
}

export function ChainGameScreen({ config, me, isHost, session, payload, busy, onAction }: Props) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const myTurn = payload.currentPlayer === me;

  return (
    <>
      <p role="status" aria-live="polite" className="sr-only">
        {over ? "The story is finished" : payload.currentPlayer ? (myTurn ? "It is your turn" : `It is ${payload.currentPlayer}'s turn`) : ""}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">One {config.piece} at a time</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start round below when everyone is here." : "Waiting for the host to start."}</p>
        </section>
      )}

      {(payload.phase === "writing" || over) && payload.opener !== "" && (
        <section aria-labelledby="story-heading" className="flex flex-col gap-4 rounded-xl border-2 border-ink bg-card p-5 shadow-ticket sm:p-6">
          <p id="story-heading" className="font-mono text-xs font-bold uppercase tracking-widest text-muted">
            {over ? "The finished story" : `Turn ${payload.turn + 1} of ${payload.totalTurns}`}
          </p>
          <p className="text-xl leading-relaxed [overflow-wrap:anywhere] sm:text-2xl">
            <span className="font-bold">{payload.opener}</span>
            {payload.entries.map((entry, i) => (
              <span key={i} title={`Added by ${entry.player}`}>
                {" "}
                {entry.prefix && <strong className="text-accent-ink">{entry.prefix} </strong>}
                {entry.text}
              </span>
            ))}
          </p>

          {payload.phase === "writing" &&
            live &&
            (myTurn ? (
              <TurnForm key={payload.turn} config={config} prefix={payload.prefix} disabled={busy} onAdd={(text) => onAction("add", { text })} />
            ) : (
              <p className="rounded-lg border-2 border-dashed border-ink px-4 py-3">
                Waiting for <strong>{payload.currentPlayer}</strong>...
              </p>
            ))}

          {isHost && live && payload.phase === "writing" && (
            <HostBar>
              <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("skip")}>
                Skip {payload.currentPlayer}
              </Button>
              <Button variant="secondary" size="sm" disabled={busy} onClick={() => onAction("end")}>
                <FlagIcon className="size-4" /> End the story
              </Button>
            </HostBar>
          )}
        </section>
      )}

      {over && <GameOver scoreboard={payload.scoreboard} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="turns" />
    </>
  );
}
