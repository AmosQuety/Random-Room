import { useState } from "react";
import { FlagIcon } from "../../components/icons";
import { ConfirmButton } from "../../components/ConfirmButton";
import { Scoreboard } from "../../components/Scoreboard";
import { fieldInputClass } from "../../components/styles";
import { Button, Eyebrow } from "../../components/ui";
import { GameOver, HostBar } from "../rounds/parts";
import type { GameScreenProps } from "../types";
import { Board } from "./board";
import { MAX_CLUE_COUNT, teamName, type SpyPayload, type Team } from "./types";

function ClueForm({ disabled, onClue }: { disabled: boolean; onClue: (word: string, count: number) => void }) {
  const [word, setWord] = useState("");
  const [count, setCount] = useState(1);
  const clean = word.trim();
  const ready = clean.length > 0 && /^\p{L}+$/u.test(clean);
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (ready && !disabled) onClue(clean, count);
      }}
      className="flex flex-col gap-3 sm:flex-row sm:items-end"
    >
      <div className="min-w-0 flex-1">
        <label htmlFor="clue-word" className="mb-1 block font-bold">
          Your one-word clue
        </label>
        <input
          id="clue-word"
          value={word}
          maxLength={24}
          autoComplete="off"
          aria-invalid={clean !== "" && !ready}
          onChange={(e) => setWord(e.target.value)}
          className={fieldInputClass}
        />
      </div>
      <div>
        <label htmlFor="clue-count" className="mb-1 block font-bold">
          Cards
        </label>
        <select id="clue-count" value={count} onChange={(e) => setCount(Number(e.target.value))} className={fieldInputClass}>
          {Array.from({ length: MAX_CLUE_COUNT }, (_, i) => i + 1).map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </div>
      <Button type="submit" variant="accent" size="lg" disabled={!ready || disabled}>
        Give clue
      </Button>
    </form>
  );
}

function Teams({ payload, me }: { payload: SpyPayload; me: string }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {(["red", "blue"] as Team[]).map((team) => (
        <section key={team} aria-label={`${teamName(team)} team`} className={`rounded-lg border-2 border-ink p-3 ${team === payload.turn ? "bg-accent-soft" : "bg-card"}`}>
          <p className="font-display text-lg font-black">
            {teamName(team)} team{payload.remaining[team] !== undefined ? ` (${payload.remaining[team]} left)` : ""}
            {team === payload.turn && <span className="ml-2 font-mono text-xs uppercase tracking-widest"> in play</span>}
          </p>
          <ul className="mt-1 text-sm">
            {payload.teams[team].map((name) => (
              <li key={name}>
                {name}
                {name === me ? " (you)" : ""}
                {payload.spymasters[team] === name ? " - spymaster" : ""}
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function roleLine(payload: SpyPayload): string {
  if (!payload.myTeam) return "";
  const team = teamName(payload.myTeam);
  return payload.isSpymaster ? `You are the ${team} spymaster. You can see the key. Give clues without saying any board word.` : `You are on the ${team} team. Work out your spymaster's clues.`;
}

function turnLine(payload: SpyPayload, me: string): string {
  if (payload.phase === "complete" || !payload.turn) return "";
  const team = teamName(payload.turn);
  if (payload.phase === "clue") return payload.spymasters[payload.turn] === me ? "Your turn: give a clue." : `Waiting for the ${team} spymaster's clue.`;
  return payload.myTeam === payload.turn && !payload.isSpymaster ? "Your team is guessing. Pick a card." : `The ${team} team is guessing.`;
}

/** The assassin ends the game for the team that found it; finding every word ends it for the team that did. */
function endLine(winner: Team, reason: NonNullable<SpyPayload["endReason"]>): string {
  const loser = winner === "red" ? "blue" : "red";
  if (reason === "assassin") return `The ${teamName(loser)} team found the assassin.`;
  if (reason === "all-found") return `The ${teamName(winner)} team found every word.`;
  return `The ${teamName(loser)} team ended early.`;
}

export function WordSpiesGameScreen({ me, isHost, session, payload, busy, onAction }: GameScreenProps<SpyPayload>) {
  const live = session.status === "Active";
  const over = session.status === "Completed" || payload.phase === "complete";
  const inPlay = payload.turn !== null && live;
  const canGuess = inPlay && payload.phase === "guessing" && payload.myTeam === payload.turn && !payload.isSpymaster;
  const myClueTurn = inPlay && payload.phase === "clue" && payload.isSpymaster && payload.myTeam === payload.turn;
  const started = payload.board.length > 0;

  return (
    <>
      <p role="status" aria-live="polite" className="sr-only">
        {over ? "Game over" : turnLine(payload, me)}
      </p>

      {session.status === "Waiting" && (
        <section className="rounded-xl border-2 border-dashed border-ink bg-card p-6 text-center">
          <Eyebrow>Ready when you are</Eyebrow>
          <p className="mt-2 font-display text-3xl font-black">Two teams, one grid</p>
          <p className="mt-1 text-muted">{isHost ? "Press Start game below to deal the teams and the board (4 or more players)." : "Waiting for the host to start."}</p>
        </section>
      )}

      {started && (
        <section aria-labelledby="spy-heading" className="flex flex-col gap-4 rounded-xl border-2 border-ink bg-card p-4 shadow-ticket sm:p-6">
          <h2 id="spy-heading" className="font-display text-xl font-black">
            {over ? (payload.winner ? `${teamName(payload.winner)} team wins` : "Game ended") : turnLine(payload, me)}
          </h2>
          {over && payload.endReason && <p className="text-sm text-muted">{payload.winner ? endLine(payload.winner, payload.endReason) : ""}</p>}
          {!over && roleLine(payload) && <p className="text-sm text-muted">{roleLine(payload)}</p>}

          {payload.clue && (
            <p className="rounded-lg border-2 border-ink bg-accent-soft px-4 py-3 font-display text-xl font-black">
              Clue: {payload.clue.word}, {payload.clue.count}
              <span className="ml-2 font-mono text-xs font-normal uppercase tracking-widest">{payload.guessesLeft} guesses left</span>
            </p>
          )}

          <Board board={payload.board} key_={payload.key} canGuess={canGuess && !busy} onGuess={(cell) => onAction("guess", { cell })} />

          {myClueTurn && <ClueForm disabled={busy} onClue={(word, count) => onAction("clue", { word, count })} />}

          {canGuess && payload.phase === "guessing" && (
            <Button variant="secondary" size="md" disabled={busy} onClick={() => onAction("pass")}>
              End our turn
            </Button>
          )}

          {payload.clueLog.length > 0 && (
            <details className="text-sm">
              <summary className="min-h-11 cursor-pointer py-2 font-semibold">Clue history</summary>
              <ol className="list-decimal pl-6">
                {payload.clueLog.map((c, i) => (
                  <li key={i}>
                    {c.word}, {c.count} ({c.by})
                  </li>
                ))}
              </ol>
            </details>
          )}

          <Teams payload={payload} me={me} />

          {isHost && live && !over && (
            <HostBar>
              <ConfirmButton
                disabled={busy}
                question="End the game and reveal the board?"
                confirmLabel="Yes, end and reveal"
                cancelLabel="Keep playing"
                onConfirm={() => onAction("end")}
              >
                <FlagIcon className="size-4" /> End and reveal
              </ConfirmButton>
            </HostBar>
          )}
        </section>
      )}

      {over && <GameOver scoreboard={payload.scoreboard} teamName={payload.winner ? teamName(payload.winner) : undefined} />}

      <Scoreboard rows={payload.scoreboard.map((s) => ({ name: s.player, score: s.score }))} me={me} unit="wins" />
    </>
  );
}
