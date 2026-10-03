import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { ForbiddenGameScreen } from "./GameScreen";
import { cardProblem, kit, splitForbidden } from "./kit";
import { forbiddenWordsModule } from "./index";
import type { ForbiddenPayload } from "./types";

const NAMES = ["Amos", "Lydia", "James", "Jacob"];
const PLAYERS: PlayerView[] = NAMES.map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<ForbiddenPayload> = {}): ForbiddenPayload {
  return {
    phase: "playing",
    round: 1,
    totalRounds: 4,
    describer: "Amos",
    judge: "Lydia",
    role: "guesser",
    card: null,
    guesses: [],
    myGuessesLeft: 25,
    result: null,
    timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
    timeLimitSeconds: null,
    scoreboard: NAMES.map((n) => ({ player: n, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: ForbiddenPayload, me: string, isHost = false) {
  const onAction = vi.fn();
  render(<ForbiddenGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

const CARD = { word: "Bicycle", forbidden: ["wheel", "pedal"] };

describe("Forbidden Words screen", () => {
  it("shows a guesser no card and lets them send a trimmed guess", () => {
    const onAction = renderScreen(payload(), "James");
    expect(screen.queryByText("Bicycle")).not.toBeInTheDocument();
    expect(screen.queryByText(/forbidden words:/i)).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Your guess"), { target: { value: "  a bike " } });
    fireEvent.click(screen.getByRole("button", { name: "Guess" }));
    expect(onAction).toHaveBeenCalledWith("guess", { text: "a bike" });
  });

  it("shows the describer the card and a skip button, but no guess box", () => {
    const onAction = renderScreen(payload({ role: "describer", card: CARD }), "Amos");
    expect(screen.getByText("Bicycle")).toBeInTheDocument();
    expect(screen.getByText("wheel")).toBeInTheDocument();
    expect(screen.queryByLabelText("Your guess")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /skip this card/i }));
    expect(onAction).toHaveBeenCalledWith("skip");
  });

  it("gives the judge the card and a flag button, and no guess box", () => {
    const onAction = renderScreen(payload({ role: "judge", card: CARD }), "Lydia");
    expect(screen.getByText("Bicycle")).toBeInTheDocument();
    expect(screen.queryByLabelText("Your guess")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /flag a slip/i }));
    expect(onAction).toHaveBeenCalledWith("flag");
  });

  it("lets the host flag without seeing the card, and never the describer", () => {
    renderScreen(payload(), "Jacob", true);
    expect(screen.getByRole("button", { name: /flag a slip/i })).toBeInTheDocument();
    expect(screen.queryByText("Bicycle")).not.toBeInTheDocument();
  });

  it("does not let a describer who is also the host flag their own round", () => {
    renderScreen(payload({ role: "describer", card: CARD }), "Amos", true);
    expect(screen.queryByRole("button", { name: /flag a slip/i })).not.toBeInTheDocument();
  });

  it("hides host controls from plain players", () => {
    renderScreen(payload(), "James", false);
    expect(screen.queryByRole("button", { name: /reveal now/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /flag a slip/i })).not.toBeInTheDocument();
  });

  it("disables guessing when no guesses are left", () => {
    renderScreen(payload({ myGuessesLeft: 0 }), "James");
    fireEvent.change(screen.getByLabelText("Your guess"), { target: { value: "x" } });
    expect(screen.getByRole("button", { name: "Guess" })).toBeDisabled();
  });

  it("shows the result and the card to everyone after the round", () => {
    renderScreen(payload({ phase: "revealed", result: { outcome: "flagged", guesser: null, word: "Bicycle", forbidden: ["wheel", "pedal"] } }), "James");
    expect(screen.getByText("Bicycle")).toBeInTheDocument();
    expect(screen.getByText(/slipped/i)).toBeInTheDocument();
  });

  it("renders guesses as plain text", () => {
    renderScreen(payload({ guesses: [{ player: "James", text: "<img src=x>" }] }), "Jacob");
    expect(screen.getByText(/<img src=x>/)).toBeInTheDocument();
    expect(document.querySelector("img")).toBeNull();
  });
});

describe("Forbidden Words setup", () => {
  it("splits and trims forbidden words", () => {
    expect(splitForbidden(" a, b ,, c ")).toEqual(["a", "b", "c"]);
  });

  it("explains what is wrong with a card", () => {
    expect(cardProblem({ word: "", forbidden: "a" })).toMatch(/word/i);
    expect(cardProblem({ word: "Cat", forbidden: "" })).toMatch(/at least one/i);
    expect(cardProblem({ word: "Cat", forbidden: "dog, CAT!" })).toMatch(/own word/i);
    expect(cardProblem({ word: "Cat", forbidden: "dog" })).toBeNull();
  });

  it("sends cards, not prompts, and keeps the round settings", () => {
    const api = forbiddenWordsModule.toApiSetup({ useBuiltIn: false, custom: [{ word: " Cat ", forbidden: "dog, mouse" }, kit.empty()], rounds: 3, timeLimit: 60 }) as Record<string, unknown>;
    expect(api).toEqual({ useBuiltIn: false, cards: [{ word: "Cat", forbidden: ["dog", "mouse"] }], rounds: 1, timeLimitSeconds: 60 });
    expect(api).not.toHaveProperty("prompts");
  });
});
