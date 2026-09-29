import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { BingoGameScreen } from "./GameScreen";
import { isBingoSetupValid, toApiBingoSetup } from "./setup";
import type { BingoPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const CARD = Array.from({ length: 25 }, (_, i) => (i === 12 ? "" : `Word${i}`));

function payload(overrides: Partial<BingoPayload> = {}): BingoPayload {
  return {
    phase: "calling",
    called: ["Word3"],
    poolSize: 40,
    myCard: CARD,
    myMarks: CARD.map((_, i) => i === 12),
    winner: null,
    winningLine: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: BingoPayload, me = "Lydia", isHost = false) {
  const onAction = vi.fn();
  render(<BingoGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Bingo screen", () => {
  it("only lets a called item be marked, and sends its cell", () => {
    const onAction = renderScreen(payload());
    expect(screen.getByRole("button", { name: /Word4, not called/ })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: /Word3, called, not marked/ }));
    expect(onAction).toHaveBeenCalledWith("mark", { cell: 3 });
  });

  it("shows marked cells as pressed and the free space as already marked", () => {
    renderScreen(payload({ myMarks: CARD.map((_, i) => i === 12 || i === 3) }));
    expect(screen.getByRole("button", { name: /Word3, marked/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /Free space, marked/ })).toBeDisabled();
  });

  it("claims bingo without sending any line", () => {
    const onAction = renderScreen(payload());
    fireEvent.click(screen.getByRole("button", { name: /bingo!/i }));
    expect(onAction).toHaveBeenCalledWith("bingo");
  });

  it("gives the host the call and end controls, and nobody else", () => {
    const onAction = renderScreen(payload(), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /call next item/i }));
    expect(onAction).toHaveBeenCalledWith("call");
    expect(screen.getByRole("button", { name: /end game/i })).toBeInTheDocument();
  });

  it("hides host controls from players", () => {
    renderScreen(payload());
    expect(screen.queryByRole("button", { name: /call next item/i })).not.toBeInTheDocument();
  });

  it("announces the latest call politely", () => {
    renderScreen(payload({ called: ["Word1", "Word9"] }));
    expect(screen.getAllByRole("status")[0]).toHaveTextContent("Called: Word9");
  });

  it("shows the winner, disables the board, and stops offering a claim", () => {
    renderScreen(payload({ phase: "complete", winner: "Lydia", winningLine: [0, 1, 2, 3, 4], scoreboard: [{ player: "Amos", score: 0 }, { player: "Lydia", score: 1 }] }));
    expect(screen.getByText(/lydia wins/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /bingo!/i })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Word3, called, not marked/ })).toBeDisabled();
  });

  it("renders no card for someone who has none", () => {
    renderScreen(payload({ myCard: null, myMarks: null }));
    expect(screen.queryByRole("group", { name: /your bingo card/i })).not.toBeInTheDocument();
  });
});

describe("Bingo setup", () => {
  it("is valid with the built-ins alone", () => expect(isBingoSetupValid({ items: [""], useBuiltIn: true })).toBe(true));

  it("needs 25 items in all without the built-ins", () => {
    const make = (n: number) => Array.from({ length: n }, (_, i) => `Item ${i}`);
    expect(isBingoSetupValid({ items: make(24), useBuiltIn: false })).toBe(false);
    expect(isBingoSetupValid({ items: make(25), useBuiltIn: false })).toBe(true);
  });

  it("rejects duplicates and sends trimmed items", () => {
    expect(isBingoSetupValid({ items: ["Same", " same "], useBuiltIn: true })).toBe(false);
    expect(toApiBingoSetup({ items: [" A ", ""], useBuiltIn: true })).toEqual({ items: ["A"], useBuiltIn: true });
  });
});
