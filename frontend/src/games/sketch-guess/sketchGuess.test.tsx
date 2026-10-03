import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { SketchGuessGameScreen } from "./GameScreen";
import { isSketchSetupValid, toApiSketchSetup } from "./setup";
import { GRID, MAX_POINTS_PER_STROKE, isFull, movedEnough, toGrid } from "./strokes";
import type { SketchPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const base: SketchPayload = {
  phase: "drawing",
  round: 1,
  totalRounds: 2,
  drawer: "Amos",
  word: null,
  strokes: [],
  guesses: [],
  myGuessesLeft: 25,
  result: null,
  timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
  timeLimitSeconds: null,
  scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
};
const props = { isHost: false, players: PLAYERS, session: ACTIVE, busy: false };

const fakeContext = () => new Proxy({}, { get: () => vi.fn(), set: () => true });

beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockImplementation(fakeContext as never);
  HTMLCanvasElement.prototype.setPointerCapture = vi.fn();
  vi.spyOn(HTMLCanvasElement.prototype, "getBoundingClientRect").mockReturnValue({ left: 0, top: 0, width: 500, height: 500 } as DOMRect);
  vi.useFakeTimers();
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

function drawLine(canvas: HTMLElement) {
  fireEvent.pointerDown(canvas, { clientX: 0, clientY: 0, pointerId: 1 });
  fireEvent.pointerMove(canvas, { clientX: 250, clientY: 250, pointerId: 1 });
  fireEvent.pointerMove(canvas, { clientX: 500, clientY: 500, pointerId: 1 });
  fireEvent.pointerUp(canvas, { pointerId: 1 });
}

describe("Sketch Guess screen", () => {
  it("shows the word and the tools to the drawer only", () => {
    const { unmount } = render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={vi.fn()} />);
    expect(screen.getByText("Bicycle")).toBeInTheDocument();
    expect(screen.getByRole("group", { name: "Drawing tools" })).toBeInTheDocument();
    expect(screen.queryByLabelText("Your guess")).not.toBeInTheDocument();
    unmount();

    render(<SketchGuessGameScreen {...props} me="Lydia" payload={base} onAction={vi.fn()} />);
    expect(screen.queryByRole("group", { name: "Drawing tools" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Your guess")).toBeInTheDocument();
  });

  it("sends a drawn stroke to the server as one batch on the 0..1000 grid", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={onAction} />);

    drawLine(screen.getByRole("img", { name: "Your drawing board" }));
    expect(onAction).not.toHaveBeenCalled();
    act(() => void vi.advanceTimersByTime(400));

    expect(onAction).toHaveBeenCalledTimes(1);
    expect(onAction).toHaveBeenCalledWith("strokes", { strokes: [{ color: 0, size: 9, points: [0, 0, 500, 500, 1000, 1000] }] });
  });

  it("keeps sends at least 100ms apart however fast the drawer draws", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={onAction} />);
    const canvas = screen.getByRole("img", { name: "Your drawing board" });

    const sentAt: number[] = [];
    onAction.mockImplementation(() => sentAt.push(Date.now()));
    for (let i = 0; i < 5; i++) {
      drawLine(canvas);
      act(() => void vi.advanceTimersByTime(30));
    }
    act(() => void vi.advanceTimersByTime(1000));

    expect(sentAt.length).toBeGreaterThan(0);
    for (let i = 1; i < sentAt.length; i++) expect(sentAt[i] - sentAt[i - 1]).toBeGreaterThanOrEqual(100);
  });

  it("uses the chosen color and pen size", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={onAction} />);

    fireEvent.click(screen.getByRole("button", { name: "Blue" }));
    fireEvent.click(screen.getByRole("button", { name: "Thick" }));
    drawLine(screen.getByRole("img", { name: "Your drawing board" }));
    act(() => void vi.advanceTimersByTime(400));

    expect(onAction).toHaveBeenCalledWith("strokes", { strokes: [expect.objectContaining({ color: 5, size: 16 })] });
  });

  it("undoes an unsent stroke locally, and asks the server to undo a sent one", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={onAction} />);
    const canvas = screen.getByRole("img", { name: "Your drawing board" });

    drawLine(canvas);
    fireEvent.click(screen.getByRole("button", { name: "Undo" }));
    act(() => void vi.advanceTimersByTime(400));
    expect(onAction).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Undo" }));
    expect(onAction).toHaveBeenCalledWith("undo");
  });

  it("clears with one action and drops anything not yet sent", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Amos" payload={{ ...base, word: "Bicycle" }} onAction={onAction} />);

    drawLine(screen.getByRole("img", { name: "Your drawing board" }));
    fireEvent.click(screen.getByRole("button", { name: "Clear" }));
    act(() => void vi.advanceTimersByTime(400));

    expect(onAction).toHaveBeenCalledTimes(1);
    expect(onAction).toHaveBeenCalledWith("clear");
  });

  it("does not let a guesser draw", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Lydia" payload={base} onAction={onAction} />);

    drawLine(screen.getByRole("img", { name: "Amos's drawing" }));
    act(() => void vi.advanceTimersByTime(400));

    expect(onAction).not.toHaveBeenCalled();
  });

  it("sends a trimmed guess and will not send an empty one", () => {
    const onAction = vi.fn();
    render(<SketchGuessGameScreen {...props} me="Lydia" payload={base} onAction={onAction} />);

    const button = screen.getByRole("button", { name: "Guess" });
    expect(button).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Your guess"), { target: { value: "  bike " } });
    fireEvent.click(button);

    expect(onAction).toHaveBeenCalledWith("guess", { text: "bike" });
  });

  it("reveals the word and who got it, and lets only the host move on", () => {
    const payload: SketchPayload = { ...base, phase: "revealed", result: { outcome: "guessed", guesser: "Lydia", word: "Bicycle" } };
    const onAction = vi.fn();
    const { unmount } = render(<SketchGuessGameScreen {...props} me="Lydia" payload={payload} onAction={onAction} />);
    expect(screen.getByText("Bicycle")).toBeInTheDocument();
    expect(screen.getByText("Lydia guessed it.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /next round/i })).not.toBeInTheDocument();
    unmount();

    render(<SketchGuessGameScreen {...props} isHost me="Amos" payload={payload} onAction={onAction} />);
    fireEvent.click(screen.getByRole("button", { name: /next round/i }));
    expect(onAction).toHaveBeenCalledWith("next");
  });

  it("renders a stranger's strokes as data, never as markup", () => {
    const payload: SketchPayload = { ...base, guesses: [{ player: "Lydia", text: "<img src=x onerror=alert(1)>" }] };
    render(<SketchGuessGameScreen {...props} me="Amos" payload={payload} onAction={vi.fn()} />);

    expect(document.querySelector("img")).toBeNull();
    expect(screen.getByText(/<img src=x/)).toBeInTheDocument();
  });
});

describe("stroke helpers", () => {
  it("maps pointer positions onto the grid and clamps at the edges", () => {
    const box = { left: 100, top: 50, width: 500, height: 500 };
    expect(toGrid(350, 300, box)).toEqual([500, 500]);
    expect(toGrid(-999, 9999, box)).toEqual([0, GRID]);
    expect(toGrid(1, 1, { ...box, width: 0 })).toEqual([0, 0]);
  });

  it("skips points that barely moved and closes a stroke at the server's point cap", () => {
    expect(movedEnough([10, 10], 11, 11)).toBe(false);
    expect(movedEnough([10, 10], 20, 10)).toBe(true);
    expect(isFull({ color: 0, size: 4, points: new Array(MAX_POINTS_PER_STROKE * 2).fill(1) })).toBe(true);
    expect(isFull({ color: 0, size: 4, points: [1, 1] })).toBe(false);
  });
});

describe("sketch setup", () => {
  const setup = (over = {}) => ({ words: [""], useBuiltIn: true, rounds: 6, timeLimitSeconds: 90, ...over });

  it("is valid with built-in words, and needs a word when they are off", () => {
    expect(isSketchSetupValid(setup())).toBe(true);
    expect(isSketchSetupValid(setup({ useBuiltIn: false }))).toBe(false);
    expect(isSketchSetupValid(setup({ useBuiltIn: false, words: ["cat"] }))).toBe(true);
  });

  it("rejects duplicate words ignoring case", () => {
    expect(isSketchSetupValid(setup({ words: ["Cat", "cat"] }))).toBe(false);
  });

  it("sends only filled, trimmed words", () => {
    expect(toApiSketchSetup(setup({ words: [" Cat ", "", "Dog"] }))).toEqual({ words: ["Cat", "Dog"], useBuiltIn: true, rounds: 6, timeLimitSeconds: 90 });
  });
});
