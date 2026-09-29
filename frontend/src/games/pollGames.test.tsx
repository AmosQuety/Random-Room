import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../lib/types";
import { GAME_LIST, GAMES } from "./registry";
import { CATEGORIES } from "./categories";
import { MostLikelyToGameScreen, type MostLikelyToPayload } from "./most-likely-to/GameScreen";
import { NeverHaveIEverGameScreen, type NeverHaveIEverPayload } from "./never-have-i-ever/GameScreen";
import { SurveyShowdownGameScreen, type SurveyShowdownPayload } from "./survey-showdown/GameScreen";
import { kit as surveyKit } from "./survey-showdown/kit";

const PLAYERS: PlayerView[] = ["Amos", "Lydia", "James"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const base = {
  phase: "collecting" as const,
  round: 1,
  totalRounds: 2,
  answered: { Amos: false, Lydia: false, James: false },
  myAnswer: null,
  result: null,
  scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
  timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
  timeLimitSeconds: null,
};
const screenProps = { me: "Amos", isHost: false, players: PLAYERS, session: ACTIVE, busy: false };

describe("Most Likely To", () => {
  const payload: MostLikelyToPayload = { ...base, prompt: { text: "Most likely to nap" } };

  it("lists everyone but me and sends the vote", () => {
    const onAction = vi.fn();
    render(<MostLikelyToGameScreen {...screenProps} payload={payload} onAction={onAction} />);

    expect(screen.queryByRole("button", { name: "Amos" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Lydia" }));

    expect(onAction).toHaveBeenCalledWith("answer", { player: "Lydia" });
  });

  it("crowns the winners at the reveal", () => {
    const revealed: MostLikelyToPayload = {
      ...base,
      phase: "revealed",
      prompt: { text: "Most likely to nap" },
      result: { text: "Most likely to nap", tally: [{ player: "Lydia", votes: 2 }, { player: "James", votes: 0 }, { player: "Amos", votes: 0 }], winners: ["Lydia"] },
    };
    render(<MostLikelyToGameScreen {...screenProps} payload={revealed} onAction={vi.fn()} />);

    expect(screen.getAllByText("Lydia")[0].closest("li")).toHaveTextContent("Crowned");
  });
});

describe("Never Have I Ever", () => {
  const payload: NeverHaveIEverPayload = { ...base, prompt: { text: "Never have I ever built a fort" } };

  it("offers two answers and sends the choice", () => {
    const onAction = vi.fn();
    render(<NeverHaveIEverGameScreen {...screenProps} payload={payload} onAction={onAction} />);

    fireEvent.click(screen.getByRole("button", { name: "I have" }));
    expect(onAction).toHaveBeenCalledWith("answer", { have: true });
  });

  it("lists confessions and who is still standing at the reveal", () => {
    const revealed: NeverHaveIEverPayload = {
      ...base,
      phase: "revealed",
      prompt: { text: "Never have I ever built a fort" },
      result: { text: "x", have: ["Amos"], never: ["Lydia", "James"] },
    };
    render(<NeverHaveIEverGameScreen {...screenProps} payload={revealed} onAction={vi.fn()} />);

    expect(screen.getByText("Never", { selector: "p" }).closest("li")).toHaveTextContent("Still standing");
    expect(screen.getByText("I have", { selector: "p" }).closest("li")).toHaveTextContent("Amos");
  });
});

describe("Survey Showdown", () => {
  const payload: SurveyShowdownPayload = { ...base, prompt: { text: "Name a pet", boardSize: 3 } };

  it("shows how many answers are on the board but not the answers", () => {
    render(<SurveyShowdownGameScreen {...screenProps} payload={payload} onAction={vi.fn()} />);
    expect(screen.getByText("3 answers are on the board.")).toBeInTheDocument();
  });

  it("will not send an empty guess, and sends a trimmed one", () => {
    const onAction = vi.fn();
    render(<SurveyShowdownGameScreen {...screenProps} payload={payload} onAction={onAction} />);

    const lockIn = screen.getByRole("button", { name: /lock it in/i });
    expect(lockIn).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Your guess"), { target: { value: "  Dog " } });
    fireEvent.click(lockIn);

    expect(onAction).toHaveBeenCalledWith("answer", { text: "Dog" });
  });

  it("reveals the board with points and who guessed each answer", () => {
    const revealed: SurveyShowdownPayload = {
      ...base,
      phase: "revealed",
      prompt: { text: "Name a pet", boardSize: 2 },
      result: {
        text: "Name a pet",
        board: [{ text: "Dog", points: 40, guessedBy: ["Amos"] }, { text: "Cat", points: 30, guessedBy: [] }],
        guesses: [{ player: "Amos", text: "dog", matched: true }, { player: "James", text: "Giraffe", matched: false }],
      },
    };
    render(<SurveyShowdownGameScreen {...screenProps} payload={revealed} onAction={vi.fn()} />);

    expect(screen.getByText("Dog").closest("li")).toHaveTextContent("40 points");
    expect(screen.getByText("Dog").closest("li")).toHaveTextContent("Top answer");
    expect(screen.getByText(/James guessed "Giraffe"/)).toBeInTheDocument();
  });

  it("needs a question and two scored answers to be a valid survey", () => {
    const row = (text: string, points: string) => ({ text, points });
    expect(surveyKit.isValid({ text: "Q", answers: [row("A", "10"), row("B", "5")] })).toBe(true);
    expect(surveyKit.isValid({ text: "Q", answers: [row("A", "10"), row("", "")] })).toBe(false);
    expect(surveyKit.isValid({ text: "Q", answers: [row("A", "10"), row("B", "0")] })).toBe(false);
    expect(surveyKit.isValid({ text: "Q", answers: [row("A", "10"), row("B", "101")] })).toBe(false);
    expect(surveyKit.isValid({ text: "", answers: [row("A", "10"), row("B", "5")] })).toBe(false);
  });

  it("sends only filled board rows with numeric points", () => {
    const body = surveyKit.toApi({ text: " Q ", answers: [{ text: " A ", points: "10" }, { text: "B", points: "5" }, { text: "", points: "" }] });
    expect(body).toEqual({ text: "Q", answers: [{ text: "A", points: 10 }, { text: "B", points: 5 }] });
  });
});

describe("the game registry", () => {
  it("has unique keys, sane player ranges and a known category for every game", () => {
    const categories = CATEGORIES.map((c) => c.key);
    expect(new Set(GAME_LIST.map((g) => g.key)).size).toBe(GAME_LIST.length);
    for (const game of GAME_LIST) {
      expect(GAMES[game.key]).toBe(game);
      expect(game.minPlayers).toBeLessThanOrEqual(game.maxPlayers);
      expect(categories).toContain(game.category);
    }
  });

  it("gives every game a distinct glyph and name", () => {
    expect(new Set(GAME_LIST.map((g) => g.name)).size).toBe(GAME_LIST.length);
    expect(new Set(GAME_LIST.map((g) => g.Glyph)).size).toBe(GAME_LIST.length);
  });

  it("every default setup is either valid or waiting for the host's input", () => {
    for (const game of GAME_LIST) expect(typeof game.isSetupValid(game.defaultSetup)).toBe("boolean");
  });
});
