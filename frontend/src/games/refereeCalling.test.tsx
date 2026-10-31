import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../lib/types";
import { BingoGameScreen } from "./bingo/GameScreen";
import type { BingoPayload } from "./bingo/types";
import { BuzzerGameScreen } from "./buzzer/GameScreen";
import type { BuzzerPayload } from "./buzzer/types";
import { SpinWheelGameScreen } from "./spin-wheel/GameScreen";
import type { WheelPayload } from "./spin-wheel/types";

const PLAYERS: PlayerView[] = ["Lydia", "James"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const refereeNote = () => screen.queryByRole("region", { name: "You are refereeing" });
const scores = [{ player: "Lydia", score: 0 }, { player: "James", score: 0 }];

describe("Spin the Wheel as the referee", () => {
  const wheel = (overrides: Partial<WheelPayload>): WheelPayload =>
    ({ phase: "ready", round: 1, totalRounds: 3, segments: ["Sing", "Dance"], spinner: "Lydia", last: null, awarded: false, awardMode: "host", scoreboard: scores, ...overrides }) as WheelPayload;

  it("has no spin button while a player is spinning", () => {
    render(<SpinWheelGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={wheel({})} busy={false} onAction={vi.fn()} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /spin|waiting for/i })).not.toBeInTheDocument();
    expect(screen.getByText("Waiting for Lydia to spin.")).toBeInTheDocument();
  });

  it("lets the referee award the point after the spin", () => {
    const onAction = vi.fn();
    const spun = wheel({ phase: "spun", last: { player: "Lydia", index: 0, label: "Sing" } });
    render(<SpinWheelGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={spun} busy={false} onAction={onAction} />);

    expect(screen.getByRole("button", { name: /award a point/i })).toBeInTheDocument();
  });

  it("still gives the spinner their button", () => {
    render(<SpinWheelGameScreen me="Lydia" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={wheel({})} busy={false} onAction={vi.fn()} />);

    expect(screen.getByRole("button", { name: /spin the wheel/i })).toBeEnabled();
  });
});

describe("Buzzer as the referee", () => {
  const buzzer = (overrides: Partial<BuzzerPayload>): BuzzerPayload =>
    ({ phase: "waiting", round: 1, totalRounds: 3, prompt: "Capital of Kenya?", buzzed: null, lockedOut: [], winner: null, scoreboard: scores, ...overrides }) as BuzzerPayload;

  it("has no buzz button but can open the buzzers", () => {
    const onAction = vi.fn();
    render(<BuzzerGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={buzzer({})} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^buzz$/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /open the buzzers/i }));
    expect(onAction).toHaveBeenCalledWith("open");
  });
});

describe("Bingo as the referee", () => {
  const bingo = { phase: "calling", called: ["Rain"], poolSize: 24, winner: null, winningLine: null, myCard: null, myMarks: null, scoreboard: scores } as unknown as BingoPayload;

  it("has no card or bingo button but can call the next item", () => {
    const onAction = vi.fn();
    render(<BingoGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={bingo} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /bingo!/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /call next item/i }));
    expect(onAction).toHaveBeenCalledWith("call");
  });

  it("does not show a card even if one were sent", () => {
    const withCard = { ...bingo, myCard: Array.from({ length: 25 }, (_, i) => `Item ${i}`), myMarks: Array(25).fill(false) } as BingoPayload;
    render(<BingoGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={withCard} busy={false} onAction={vi.fn()} />);

    expect(screen.queryByRole("group", { name: /your bingo card/i })).not.toBeInTheDocument();
  });
});
