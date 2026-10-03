import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { SpinWheelGameScreen } from "./GameScreen";
import { isWheelSetupValid, toApiWheelSetup } from "./setup";
import type { WheelPayload, WheelSetup } from "./types";
import { rotationFor } from "./wheelMath";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<WheelPayload> = {}): WheelPayload {
  return {
    phase: "ready",
    round: 1,
    totalRounds: 2,
    segments: ["Sing", "Dance", "Joke"],
    spinner: "Amos",
    last: null,
    awarded: false,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: WheelPayload, me = "Amos", isHost = false) {
  const onAction = vi.fn();
  render(<SpinWheelGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Spin the Wheel screen", () => {
  it("lets only the spinner spin, and sends no result of its own", () => {
    const onAction = renderScreen(payload());
    fireEvent.click(screen.getByRole("button", { name: /spin the wheel/i }));
    expect(onAction).toHaveBeenCalledWith("spin");
  });

  it("disables the button for everyone else and names who is up", () => {
    renderScreen(payload(), "Lydia");
    expect(screen.getByRole("button", { name: /waiting for amos/i })).toBeDisabled();
    expect(screen.getByText(/amos's turn/i)).toBeInTheDocument();
  });

  it("announces the server's result and shows host controls once spun", () => {
    const onAction = renderScreen(payload({ phase: "spun", last: { player: "Amos", index: 1, label: "Dance" } }), "Amos", true);
    expect(screen.getByRole("status")).toHaveTextContent("Amos landed on: Dance");
    fireEvent.click(screen.getByRole("button", { name: /award a point/i }));
    expect(onAction).toHaveBeenCalledWith("award");
    fireEvent.click(screen.getByRole("button", { name: /next spin/i }));
    expect(onAction).toHaveBeenCalledWith("next");
  });

  describe("while the wheel is turning", () => {
    afterEach(() => vi.useRealTimers());

    function renderAsHost(p: WheelPayload) {
      const ui = (next: WheelPayload) => (
        <SpinWheelGameScreen me="Amos" isHost players={PLAYERS} session={ACTIVE} payload={next} busy={false} onAction={vi.fn()} />
      );
      const view = render(ui(p));
      return (next: WheelPayload) => view.rerender(ui(next));
    }

    it("holds back the result of every spin, not only the first", () => {
      vi.useFakeTimers();
      const show = renderAsHost(payload({ totalRounds: 3 }));
      const spun = (round: number) => payload({ phase: "spun", round, totalRounds: 3, last: { player: "Amos", index: 1, label: "Dance" } });

      show(spun(1));
      act(() => void vi.advanceTimersByTime(4000));
      expect(screen.getByRole("button", { name: /next spin/i })).toBeEnabled();

      show(payload({ round: 2, totalRounds: 3 }));
      show(spun(2));

      expect(screen.getByRole("status")).toHaveClass("sr-only");
      // Not announced early to screen readers either.
      expect(screen.getByRole("status")).toBeEmptyDOMElement();
      expect(screen.getByRole("button", { name: /next spin/i })).toBeDisabled();
      expect(screen.getByRole("button", { name: /award a point/i })).toBeDisabled();

      act(() => void vi.advanceTimersByTime(4000));
      expect(screen.getByRole("status")).not.toHaveClass("sr-only");
      expect(screen.getByRole("status")).toHaveTextContent("Amos landed on: Dance");
      expect(screen.getByRole("button", { name: /next spin/i })).toBeEnabled();
    });
  });

  it("does not offer host controls to a player", () => {
    renderScreen(payload({ phase: "spun", last: { player: "Amos", index: 1, label: "Dance" } }), "Lydia", false);
    expect(screen.queryByRole("button", { name: /award a point/i })).not.toBeInTheDocument();
  });

  it("offers the final spin as finishing the game", () => {
    renderScreen(payload({ phase: "spun", round: 2, last: { player: "Lydia", index: 0, label: "Sing" }, spinner: "Lydia" }), "Amos", true);
    expect(screen.getByRole("button", { name: /finish game/i })).toBeInTheDocument();
  });

  it("stops the disc with the chosen segment under the pointer", () => {
    const count = 4;
    for (let index = 0; index < count; index++) {
      const middleOfSegment = ((index + 0.5) * 360) / count;
      expect((rotationFor(index, count) + middleOfSegment) % 360).toBeCloseTo(0);
    }
  });

  it("shows game over when complete", () => {
    renderScreen(payload({ phase: "complete", spinner: null }));
    expect(screen.getByText(/game over/i)).toBeInTheDocument();
  });
});

describe("Spin the Wheel setup", () => {
  const setup = (patch: Partial<WheelSetup>): WheelSetup => ({ segments: [""], useBuiltIn: true, spins: 6, ...patch });

  it("is valid with only the built-in challenges", () => expect(isWheelSetupValid(setup({}))).toBe(true));

  it("needs two segments when the built-ins are off", () => {
    expect(isWheelSetupValid(setup({ useBuiltIn: false, segments: ["One"] }))).toBe(false);
    expect(isWheelSetupValid(setup({ useBuiltIn: false, segments: ["One", "Two"] }))).toBe(true);
  });

  it("rejects duplicates and too many segments", () => {
    expect(isWheelSetupValid(setup({ segments: ["Same", " same "] }))).toBe(false);
    expect(isWheelSetupValid(setup({ segments: Array.from({ length: 13 }, (_, i) => `S${i}`) }))).toBe(false);
  });

  it("sends trimmed, non-empty segments", () => {
    expect(toApiWheelSetup(setup({ segments: [" A ", "", "B"], spins: 3 }))).toEqual({ segments: ["A", "B"], useBuiltIn: true, spins: 3 });
  });
});
