import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { BuzzerGameScreen } from "./GameScreen";
import { isBuzzerSetupValid, toApiBuzzerSetup } from "./setup";
import type { BuzzerPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia", "James"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<BuzzerPayload> = {}): BuzzerPayload {
  return {
    phase: "open",
    round: 1,
    totalRounds: 2,
    prompt: "Name a fruit",
    buzzed: null,
    lockedOut: [],
    winner: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: BuzzerPayload, me: string, isHost = false) {
  const onAction = vi.fn();
  render(<BuzzerGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Buzzer screen", () => {
  it("lets a player buzz while the buzzers are open", () => {
    const onAction = renderScreen(payload(), "Lydia");
    fireEvent.click(screen.getByRole("button", { name: "BUZZ" }));
    expect(onAction).toHaveBeenCalledWith("buzz");
  });

  it("disables the buzzer before opening and after someone else buzzed", () => {
    renderScreen(payload({ phase: "waiting" }), "Lydia");
    expect(screen.getByRole("button", { name: "Buzz" })).toBeDisabled();
  });

  it("names who buzzed first and keeps everyone else locked out", () => {
    renderScreen(payload({ buzzed: "James" }), "Lydia");
    expect(screen.getByRole("status")).toHaveTextContent("James buzzed first.");
    expect(screen.getByRole("button", { name: "Buzz" })).toBeDisabled();
  });

  it("locks out a player who already missed", () => {
    renderScreen(payload({ lockedOut: ["Lydia"] }), "Lydia");
    expect(screen.getByRole("status")).toHaveTextContent(/you missed/i);
    expect(screen.getByRole("button", { name: "Buzz" })).toBeDisabled();
  });

  it("gives the host judging buttons only once someone buzzed, and no buzzer of their own", () => {
    const onAction = renderScreen(payload({ buzzed: "Lydia" }), "Amos", true);
    expect(screen.queryByRole("button", { name: /buzz/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Correct" }));
    fireEvent.click(screen.getByRole("button", { name: "Wrong" }));
    expect(onAction.mock.calls).toEqual([["correct"], ["wrong"]]);
  });

  it("lets the host open the buzzers, and hides judging before a buzz", () => {
    const onAction = renderScreen(payload({ phase: "waiting" }), "Amos", true);
    expect(screen.queryByRole("button", { name: "Correct" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /open the buzzers/i }));
    expect(onAction).toHaveBeenCalledWith("open");
  });

  it("does not show host controls to players", () => {
    renderScreen(payload({ buzzed: "Lydia" }), "James");
    expect(screen.queryByRole("button", { name: "Correct" })).not.toBeInTheDocument();
  });

  it("announces the winner and lets the host finish on the last round", () => {
    renderScreen(payload({ phase: "resolved", round: 2, winner: "Lydia" }), "Amos", true);
    expect(screen.getByRole("status")).toHaveTextContent("Lydia got it.");
    expect(screen.getByRole("button", { name: /finish game/i })).toBeInTheDocument();
  });
});

describe("Buzzer setup", () => {
  it("is valid with built-ins or at least one prompt", () => {
    expect(isBuzzerSetupValid({ prompts: [""], useBuiltIn: true, rounds: 8 })).toBe(true);
    expect(isBuzzerSetupValid({ prompts: ["  "], useBuiltIn: false, rounds: 8 })).toBe(false);
    expect(isBuzzerSetupValid({ prompts: ["Name a bird"], useBuiltIn: false, rounds: 8 })).toBe(true);
  });

  it("sends trimmed prompts", () => {
    expect(toApiBuzzerSetup({ prompts: [" A ", ""], useBuiltIn: false, rounds: 3 })).toEqual({ prompts: ["A"], useBuiltIn: false, rounds: 3 });
  });
});
