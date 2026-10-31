import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../lib/types";
import { ForbiddenGameScreen } from "./forbidden-words/GameScreen";
import type { ForbiddenPayload } from "./forbidden-words/types";
import { SketchGuessGameScreen } from "./sketch-guess/GameScreen";
import type { SketchPayload } from "./sketch-guess/types";
import { WordSpiesGameScreen } from "./word-spies/GameScreen";
import type { SpyPayload } from "./word-spies/types";

const PLAYERS: PlayerView[] = ["Lydia", "James", "Ben", "Sam"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const refereeNote = () => screen.queryByRole("region", { name: "You are refereeing" });
const scores = PLAYERS.map((p) => ({ player: p.name, score: 0 }));
const timer = { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" };

describe("Word Spies as the referee", () => {
  const spies: SpyPayload = {
    phase: "guessing",
    board: Array.from({ length: 25 }, (_, i) => ({ word: `word${i}`, owner: null })),
    teams: { red: ["Lydia", "James"], blue: ["Ben", "Sam"] },
    spymasters: { red: "Lydia", blue: "Ben" },
    myTeam: null,
    isSpymaster: false,
    key: null,
    turn: "red",
    clue: { word: "fruit", count: 2, by: "Lydia" },
    guessesLeft: 3,
    remaining: { red: 9, blue: 8 },
    winner: null,
    endReason: null,
    clueLog: [],
    scoreboard: scores,
  };

  it("cannot guess, pass or give a clue, but can end the game", () => {
    const onAction = vi.fn();
    render(<WordSpiesGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={{ ...spies, myTeam: "red" }} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    for (const card of screen.queryAllByRole("button", { name: /word\d+/ })) expect(card).toBeDisabled();
    expect(screen.queryByRole("button", { name: /end our turn/i })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /end and reveal/i })).toBeInTheDocument();
  });
});

describe("Forbidden Words as the referee", () => {
  const forbidden: ForbiddenPayload = {
    phase: "playing",
    round: 1,
    totalRounds: 3,
    describer: "Lydia",
    judge: "James",
    role: "referee",
    card: { word: "Banana", forbidden: ["Yellow", "Fruit"] },
    guesses: [],
    myGuessesLeft: 0,
    result: null,
    timer,
    timeLimitSeconds: null,
    scoreboard: scores,
  };

  it("sees the card and can flag a slip, but has no guess box", () => {
    const onAction = vi.fn();
    render(<ForbiddenGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={forbidden} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.getByText("Banana")).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /flag a slip/i }));
    expect(onAction).toHaveBeenCalledWith("flag");
  });

  it("does not offer a flag to a guesser", () => {
    render(<ForbiddenGameScreen me="Ben" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={{ ...forbidden, role: "guesser", card: null, myGuessesLeft: 3 }} busy={false} onAction={vi.fn()} />);

    expect(screen.queryByRole("button", { name: /flag a slip/i })).not.toBeInTheDocument();
  });
});

describe("Sketch Guess as the referee", () => {
  const sketch: SketchPayload = {
    phase: "drawing",
    round: 1,
    totalRounds: 3,
    drawer: "Lydia",
    word: null,
    strokes: [],
    guesses: [],
    myGuessesLeft: 0,
    result: null,
    timer,
    timeLimitSeconds: null,
    scoreboard: scores,
  };

  it("has no guess box and can reveal", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={sketch} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /reveal now/i }));
    expect(onAction).toHaveBeenCalledWith("reveal");
  });

  it("still gives a guesser the guess box", () => {
    render(<SketchGuessGameScreen me="Ben" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={{ ...sketch, myGuessesLeft: 3 }} busy={false} onAction={vi.fn()} />);

    expect(screen.getByRole("textbox")).toBeInTheDocument();
  });
});
