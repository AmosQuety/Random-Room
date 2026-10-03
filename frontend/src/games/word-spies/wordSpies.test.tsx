import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { WordSpiesGameScreen } from "./GameScreen";
import { isSpySetupValid, toApiSpySetup } from "./setup";
import type { Owner, SpyPayload } from "./types";

const NAMES = ["Amos", "Lydia", "James", "Jacob"];
const PLAYERS: PlayerView[] = NAMES.map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const KEY: Owner[] = Array.from({ length: 25 }, (_, i) => (i === 0 ? "assassin" : i < 10 ? "red" : i < 18 ? "blue" : "neutral"));

function payload(overrides: Partial<SpyPayload> = {}): SpyPayload {
  return {
    phase: "guessing",
    board: Array.from({ length: 25 }, (_, i) => ({ word: `word${i}`, owner: null })),
    teams: { red: ["Amos", "Lydia"], blue: ["James", "Jacob"] },
    spymasters: { red: "Amos", blue: "James" },
    myTeam: "red",
    isSpymaster: false,
    key: null,
    turn: "red",
    clue: { word: "animals", count: 2, by: "Amos" },
    guessesLeft: 3,
    remaining: { red: 9, blue: 8 },
    winner: null,
    endReason: null,
    clueLog: [{ word: "animals", count: 2, by: "Amos" }],
    scoreboard: NAMES.map((n) => ({ player: n, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: SpyPayload, me: string, isHost = false) {
  const onAction = vi.fn();
  render(<WordSpiesGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Word Spies screen", () => {
  it("lets an operative of the team in play turn over a card", () => {
    const onAction = renderScreen(payload(), "Lydia");
    fireEvent.click(screen.getByRole("button", { name: "word5" }));
    expect(onAction).toHaveBeenCalledWith("guess", { cell: 5 });
  });

  it("does not let the other team or the spymaster guess", () => {
    renderScreen(payload({ myTeam: "blue" }), "Jacob");
    expect(screen.getByRole("button", { name: "word5" })).toBeDisabled();
  });

  it("keeps the spymaster from guessing but shows them the key", () => {
    renderScreen(payload({ isSpymaster: true, key: KEY }), "Amos");
    expect(screen.getByRole("button", { name: /word0, Assassin \(secret\)/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /word12, Blue \(secret\)/ })).toBeInTheDocument();
  });

  it("shows an operative no owner for any unturned card", () => {
    renderScreen(payload(), "Lydia");
    expect(screen.queryByText(/secret/)).not.toBeInTheDocument();
    expect(screen.queryByText("Assassin")).not.toBeInTheDocument();
  });

  it("says who a turned card belongs to in words, not colour alone", () => {
    const board = payload().board.map((c, i) => (i === 3 ? { ...c, owner: "blue" as Owner } : c));
    renderScreen(payload({ board }), "Lydia");
    expect(screen.getByRole("button", { name: /word3, turned over: Blue/ })).toBeDisabled();
  });

  it("gives the spymaster of the team in play a clue form that sends a single word and a count", () => {
    const onAction = renderScreen(payload({ phase: "clue", clue: null, isSpymaster: true, key: KEY }), "Amos");
    const send = screen.getByRole("button", { name: /give clue/i });
    fireEvent.change(screen.getByLabelText(/your one-word clue/i), { target: { value: "two words" } });
    expect(send).toBeDisabled();
    fireEvent.change(screen.getByLabelText(/your one-word clue/i), { target: { value: " ocean " } });
    fireEvent.change(screen.getByLabelText(/cards/i), { target: { value: "3" } });
    fireEvent.click(send);
    expect(onAction).toHaveBeenCalledWith("clue", { word: "ocean", count: 3 });
  });

  it("does not offer the clue form to the other team's spymaster", () => {
    renderScreen(payload({ phase: "clue", clue: null, isSpymaster: true, key: KEY, myTeam: "blue" }), "James");
    expect(screen.queryByLabelText(/your one-word clue/i)).not.toBeInTheDocument();
  });

  it("lets the operatives end their turn", () => {
    const onAction = renderScreen(payload(), "Lydia");
    fireEvent.click(screen.getByRole("button", { name: /end our turn/i }));
    expect(onAction).toHaveBeenCalledWith("pass");
  });

  it("lets the host end the game, but only after confirming", () => {
    const onAction = renderScreen(payload(), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /end and reveal/i }));
    expect(onAction).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: /yes, end and reveal/i }));
    expect(onAction).toHaveBeenCalledWith("end");
  });

  it("gives nobody but the host an end button", () => {
    renderScreen(payload(), "Lydia");
    expect(screen.queryByRole("button", { name: /end and reveal/i })).not.toBeInTheDocument();
  });

  // jsdom has no layout, so this guards the classes; sizes and wrapping were checked in a real browser at 390px.
  it("keeps card words readable on a phone and lets a long word wrap inside its card", () => {
    renderScreen(payload(), "Lydia");
    const card = screen.getByRole("button", { name: /word3/i });
    expect(card.className).toContain("text-[0.8rem]");
    expect(card.className).toContain("min-w-0");
    expect(card.className).toContain("[overflow-wrap:anywhere]");
  });

  it("names the winning team as the one that found every word", () => {
    const board = KEY.map((owner, i) => ({ word: `word${i}`, owner }));
    renderScreen(payload({ phase: "complete", board, key: KEY, winner: "blue", endReason: "all-found", turn: null, clue: null }), "Lydia");
    expect(screen.getByText("The Blue team found every word.")).toBeInTheDocument();
  });

  it("names the losing team as the one that found the assassin", () => {
    const board = KEY.map((owner, i) => ({ word: `word${i}`, owner }));
    renderScreen(payload({ phase: "complete", board, key: KEY, winner: "blue", endReason: "assassin", turn: null, clue: null }), "Lydia");
    expect(screen.getByText("The Red team found the assassin.")).toBeInTheDocument();
  });

  it("shows the winner and the whole board once the game is over", () => {
    const board = payload().board.map((c, i) => ({ ...c, owner: KEY[i] }));
    renderScreen(payload({ phase: "complete", board, key: KEY, winner: "blue", endReason: "assassin", turn: null, clue: null }), "Lydia");
    expect(screen.getByRole("heading", { name: /blue team wins/i })).toBeInTheDocument();
    expect(screen.getByText(/red team found the assassin/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /word0, turned over: Assassin/ })).toBeDisabled();
  });
});

describe("Word Spies setup", () => {
  it("is valid with the built-in words alone", () => expect(isSpySetupValid({ words: [""], useBuiltIn: true })).toBe(true));

  it("needs 25 words without the built-ins", () => {
    const make = (n: number) => Array.from({ length: n }, (_, i) => `w${i}`);
    expect(isSpySetupValid({ words: make(24), useBuiltIn: false })).toBe(false);
    expect(isSpySetupValid({ words: make(25), useBuiltIn: false })).toBe(true);
  });

  it("rejects duplicates and sends trimmed words", () => {
    expect(isSpySetupValid({ words: ["Same", " same "], useBuiltIn: true })).toBe(false);
    expect(toApiSpySetup({ words: [" a ", ""], useBuiltIn: true })).toEqual({ words: ["a"], useBuiltIn: true });
  });
});
