import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { fortunatelyConfig, fortunatelyModule } from "../fortunately";
import { oneWordConfig, oneWordStoryModule } from "../one-word-story";
import { ChainGameScreen } from "./GameScreen";
import { entryText } from "./storyText";
import { isChainSetupValid, toApiChainSetup } from "./setup";
import type { ChainConfig, ChainPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<ChainPayload> = {}): ChainPayload {
  return {
    phase: "writing",
    opener: "Once upon a time, there was a",
    entries: [{ player: "Lydia", text: "dragon", prefix: "" }],
    turn: 1,
    totalTurns: 10,
    currentPlayer: "Amos",
    prefix: "",
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(config: ChainConfig, p: ChainPayload, me = "Amos", isHost = false) {
  const onAction = vi.fn();
  render(<ChainGameScreen config={config} me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Story screen", () => {
  it("shows the story so far and lets the current player add a trimmed word", () => {
    const onAction = renderScreen(oneWordConfig, payload());
    expect(screen.getByText(/Once upon a time, there was a/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/add one word/i), { target: { value: "  wizard " } });
    fireEvent.click(screen.getByRole("button", { name: /add it/i }));
    expect(onAction).toHaveBeenCalledWith("add", { text: "wizard" });
  });

  it("refuses more than one word before sending", () => {
    const onAction = renderScreen(oneWordConfig, payload());
    fireEvent.change(screen.getByLabelText(/add one word/i), { target: { value: "two words" } });
    expect(screen.getByRole("button", { name: /add it/i })).toBeDisabled();
    expect(screen.getByText(/just one word per turn/i)).toBeInTheDocument();
    expect(onAction).not.toHaveBeenCalled();
  });

  it("makes everyone else wait, with no input", () => {
    renderScreen(oneWordConfig, payload({ currentPlayer: "Lydia" }));
    expect(screen.queryByLabelText(/add one word/i)).not.toBeInTheDocument();
    expect(screen.getByText(/waiting for/i)).toHaveTextContent("Lydia");
  });

  it("shows the server's lead-in before the sentence and marks earlier ones", () => {
    renderScreen(fortunatelyConfig, payload({ opener: "Sam baked a cake.", entries: [{ player: "Lydia", text: "it smelled amazing", prefix: "Fortunately," }], prefix: "Unfortunately," }));
    expect(screen.getByText("Fortunately,")).toBeInTheDocument();
    expect(screen.getByText("Unfortunately,")).toBeInTheDocument();
    expect(screen.getByText(/it smelled amazing/)).toBeInTheDocument();
  });

  it("gives only the host skip and end controls", () => {
    const onAction = renderScreen(oneWordConfig, payload(), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /skip amos/i }));
    fireEvent.click(screen.getByRole("button", { name: /end the story/i }));
    expect(onAction.mock.calls).toEqual([["skip"], ["end"]]);
  });

  it("hides host controls from players", () => {
    renderScreen(oneWordConfig, payload({ currentPlayer: "Lydia" }), "Amos", false);
    expect(screen.queryByRole("button", { name: /end the story/i })).not.toBeInTheDocument();
  });

  it("shows the finished story and game over", () => {
    renderScreen(oneWordConfig, payload({ phase: "complete", currentPlayer: null, prefix: null }));
    expect(screen.getByText(/the finished story/i)).toBeInTheDocument();
    expect(screen.getByText(/game over/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/add one word/i)).not.toBeInTheDocument();
  });

  it("renders the writers' text as plain text", () => {
    renderScreen(oneWordConfig, payload({ entries: [{ player: "Lydia", text: "<img src=x>", prefix: "" }] }));
    expect(screen.getByText(/<img src=x>/)).toBeInTheDocument();
    expect(document.querySelector("img")).toBeNull();
  });
});

describe("Story setup", () => {
  it("is valid with the built-ins or at least one opener", () => {
    expect(isChainSetupValid({ openers: [""], useBuiltIn: true, length: 10 })).toBe(true);
    expect(isChainSetupValid({ openers: [" "], useBuiltIn: false, length: 10 })).toBe(false);
    expect(isChainSetupValid({ openers: ["Once"], useBuiltIn: false, length: 10 })).toBe(true);
  });

  it("sends trimmed openers and the chosen length", () => {
    expect(toApiChainSetup({ openers: [" A ", ""], useBuiltIn: false, length: 8 })).toEqual({ openers: ["A"], useBuiltIn: false, length: 8 });
  });

  it("gives each game its own defaults, category and accent", () => {
    expect(oneWordStoryModule.defaultSetup.length).toBe(40);
    expect(fortunatelyModule.defaultSetup.length).toBe(12);
    expect(oneWordStoryModule.category).toBe("story");
    expect(fortunatelyModule.accent).not.toBe(oneWordStoryModule.accent);
  });

  it("joins a lead-in and a sentence", () => {
    expect(entryText({ player: "A", text: "we ran", prefix: "Fortunately," })).toBe("Fortunately, we ran");
    expect(entryText({ player: "A", text: "word", prefix: "" })).toBe("word");
  });
});
