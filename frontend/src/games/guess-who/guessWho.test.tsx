import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { GuessWhoGameScreen } from "./GameScreen";
import type { GuessWhoPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia", "James"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<GuessWhoPayload> = {}): GuessWhoPayload {
  return {
    phase: "voting",
    round: 1,
    totalRounds: 3,
    text: "I once met a president",
    isMine: false,
    submitted: { Amos: false, Lydia: false, James: false },
    myFact: null,
    guessCount: 1,
    guessesNeeded: 2,
    myGuess: null,
    result: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: GuessWhoPayload, me = "Amos", isHost = false) {
  const onAction = vi.fn();
  render(<GuessWhoGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Guess Who screen", () => {
  it("collects a fact and sends it trimmed", () => {
    const onAction = renderScreen(payload({ phase: "submitting", text: null }));
    const lockIn = screen.getByRole("button", { name: /lock it in/i });
    expect(lockIn).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/something others might not know/i), { target: { value: "  I met a president " } });
    fireEvent.click(lockIn);

    expect(onAction).toHaveBeenCalledWith("submit", { text: "I met a president" });
  });

  it("shows my own fact back to me and who else has submitted", () => {
    renderScreen(payload({ phase: "submitting", text: null, myFact: "I met a president", submitted: { Amos: true, Lydia: false, James: true } }));
    expect(screen.getByText("I met a president")).toBeInTheDocument();
    expect(screen.getByText("2 of 3 submitted")).toBeInTheDocument();
  });

  it("only lets the host begin once at least two facts are in", () => {
    renderScreen(payload({ phase: "submitting", text: null, submitted: { Amos: true, Lydia: false, James: false } }), "Amos", true);
    expect(screen.getByRole("button", { name: /begin with 1 fact/i })).toBeDisabled();
  });

  it("lets the host begin with enough facts", () => {
    const onAction = renderScreen(payload({ phase: "submitting", text: null, submitted: { Amos: true, Lydia: true, James: false } }), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /begin with 2 facts/i }));
    expect(onAction).toHaveBeenCalledWith("begin");
  });

  it("offers everyone but me as a guess and sends the choice", () => {
    const onAction = renderScreen(payload());
    expect(screen.queryByRole("button", { name: "Amos" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Lydia" }));
    expect(onAction).toHaveBeenCalledWith("guess", { player: "Lydia" });
  });

  it("tells the author it is theirs and offers no guess buttons", () => {
    renderScreen(payload({ isMine: true }));
    expect(screen.getByText(/this one is yours/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Lydia" })).not.toBeInTheDocument();
  });

  it("locks a guess in", () => {
    renderScreen(payload({ myGuess: "Lydia" }));
    expect(screen.getByText(/you guessed lydia/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "James" })).not.toBeInTheDocument();
  });

  it("shows progress as a count, never as names", () => {
    renderScreen(payload());
    expect(screen.getByText("1 of 2 have guessed")).toBeInTheDocument();
  });

  it("reveals the author and who guessed right", () => {
    renderScreen(payload({ phase: "revealed", result: { author: "James", text: "x", guesses: [{ player: "Amos", guessed: "James" }, { player: "Lydia", guessed: "Amos" }], correct: ["Amos"] } }));
    expect(screen.getByText(/written by/i)).toHaveTextContent("Written by James");
    expect(screen.getByText(/guessed it: amos/i)).toBeInTheDocument();
  });

  it("offers the host reveal, next and finish at the right times", () => {
    const reveal = renderScreen(payload(), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /reveal now/i }));
    expect(reveal).toHaveBeenCalledWith("reveal");
  });

  it("does not show host controls to other players", () => {
    renderScreen(payload());
    expect(screen.queryByRole("button", { name: /reveal now|next fact|finish game/i })).not.toBeInTheDocument();
  });
});
