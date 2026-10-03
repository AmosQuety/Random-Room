import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { TwoWayGameScreen } from "./TwoWay";
import type { TwoWayPayload } from "./twoWay";

const PLAYERS: PlayerView[] = ["Amos", "Lydia", "James"].map((name) => ({ name, online: true, claimed: true }));

const session = (status: SessionView["status"]): SessionView => ({ id: "s", number: 1, status, startedAt: null, endedAt: null });

function payload(overrides: Partial<TwoWayPayload> = {}): TwoWayPayload {
  return {
    phase: "collecting",
    round: 1,
    totalRounds: 3,
    prompt: { options: ["Pizza", "Tacos"] },
    answered: { Amos: false, Lydia: true, James: false },
    myAnswer: null,
    result: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
    timeLimitSeconds: null,
    ...overrides,
  };
}

function renderScreen(p: TwoWayPayload, options: { me?: string; isHost?: boolean; status?: SessionView["status"]; onAction?: () => void } = {}) {
  const onAction = options.onAction ?? vi.fn();
  render(
    <TwoWayGameScreen
      lead="Would you rather..."
      me={options.me ?? "Amos"}
      isHost={options.isHost ?? false}
      players={PLAYERS}
      session={session(options.status ?? "Active")}
      payload={p}
      busy={false}
      onAction={onAction}
    />,
  );
  return onAction;
}

const revealed = (): TwoWayPayload =>
  payload({
    phase: "revealed",
    answered: { Amos: true, Lydia: true, James: true },
    myAnswer: { choice: 0 },
    result: { options: ["Pizza", "Tacos"], voters: [["Amos", "Lydia"], ["James"]], counts: [2, 1], majority: 0 },
    scoreboard: [
      { player: "Amos", score: 1 },
      { player: "Lydia", score: 1 },
      { player: "James", score: 0 },
    ],
  });

describe("RoundGameScreen (through Would You Rather)", () => {
  it("tells everyone the game is waiting to start", () => {
    renderScreen(payload({ phase: "lobby", prompt: null }), { status: "Waiting" });
    expect(screen.getByText(/waiting for the host/i)).toBeInTheDocument();
    expect(screen.getByText("3 rounds")).toBeInTheDocument();
  });

  it("shows the two options and sends the chosen one", () => {
    const onAction = renderScreen(payload());
    fireEvent.click(screen.getByRole("button", { name: /tacos/i }));
    expect(onAction).toHaveBeenCalledWith("answer", { choice: 1 });
  });

  it("locks the input after answering and never shows anyone's pick or the counts", () => {
    renderScreen(payload({ myAnswer: { choice: 1 }, answered: { Amos: true, Lydia: true, James: false } }));
    expect(screen.getByText(/locked in/i)).toBeInTheDocument();
    expect(screen.getByText(/you picked: tacos/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /pizza/i })).not.toBeInTheDocument();
    expect(screen.queryByText("Majority")).not.toBeInTheDocument();
    expect(screen.getByText("2 of 3 answered")).toBeInTheDocument();
  });

  it("tells the group when the round is waiting on someone who has not joined yet", () => {
    const players: PlayerView[] = [
      { name: "Amos", online: true, claimed: true },
      { name: "Lydia", online: true, claimed: true },
      { name: "James", online: false, claimed: false },
    ];
    render(
      <TwoWayGameScreen lead="Would you rather..." me="Amos" isHost players={players} session={session("Active")} payload={payload()} busy={false} onAction={vi.fn()} />,
    );

    expect(screen.getByText(/waiting for james to open their invite link/i)).toBeInTheDocument();
    const chip = within(screen.getByRole("list", { name: "Answer progress" }))
      .getAllByRole("listitem")
      .find((item) => item.textContent?.includes("James"));
    expect(chip).toHaveTextContent(/not joined/i);
    expect(chip).toHaveTextContent(/has not joined yet/i);
  });

  it("says nothing about joining when everyone has joined", () => {
    renderScreen(payload());
    expect(screen.queryByText(/invite link/i)).not.toBeInTheDocument();
  });

  it("says who has and has not answered in words, not only colour", () => {
    renderScreen(payload());
    const progress = within(screen.getByRole("list", { name: "Answer progress" }));
    expect(progress.getByText(/lydia/i).closest("li")).toHaveTextContent("has answered");
    expect(progress.getByText(/james/i).closest("li")).toHaveTextContent("has not answered yet");
  });

  it("reveals the counts, the majority and who chose what", () => {
    renderScreen(revealed());
    const majority = screen.getByText("Pizza").closest("li")!;
    expect(majority).toHaveTextContent("2 votes");
    expect(majority).toHaveTextContent("Majority");
    expect(majority).toHaveTextContent("Amos, Lydia");
    expect(majority).toHaveTextContent("Your pick");
    expect(screen.getByText("Tacos").closest("li")).toHaveTextContent("1 vote");
  });

  it("gives the host reveal and next-round controls, and hides them from everyone else", () => {
    const onAction = renderScreen(payload(), { isHost: true });
    fireEvent.click(screen.getByRole("button", { name: /reveal now/i }));
    expect(onAction).toHaveBeenCalledWith("reveal");
  });

  it("offers the host 'Next round' after a reveal, and 'Finish game' on the last one", () => {
    const first = renderScreen(revealed(), { isHost: true });
    fireEvent.click(screen.getByRole("button", { name: /next round/i }));
    expect(first).toHaveBeenCalledWith("next");
  });

  it("offers 'Finish game' on the final round", () => {
    renderScreen({ ...revealed(), round: 3 }, { isHost: true });
    expect(screen.getByRole("button", { name: /finish game/i })).toBeInTheDocument();
  });

  it("does not show host controls to a regular player", () => {
    renderScreen(revealed());
    expect(screen.queryByRole("button", { name: /next round|reveal now|finish game/i })).not.toBeInTheDocument();
  });

  it("names the winner when the game is over and keeps the scoreboard", () => {
    renderScreen({ ...revealed(), phase: "complete", scoreboard: [{ player: "Amos", score: 2 }, { player: "Lydia", score: 1 }, { player: "James", score: 0 }] }, { status: "Completed" });
    expect(screen.getByText("Amos wins")).toBeInTheDocument();
    expect(within(screen.getByRole("region", { name: /scoreboard/i })).getAllByRole("listitem")).toHaveLength(3);
  });

  it("calls a shared top score a tie", () => {
    renderScreen({ ...revealed(), phase: "complete" }, { status: "Completed" });
    expect(screen.getByText("Amos & Lydia tie")).toBeInTheDocument();
  });

  it("says nobody scored when everyone is on zero", () => {
    renderScreen({ ...revealed(), phase: "complete", scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })) }, { status: "Completed" });
    expect(screen.getByText("Nobody scored")).toBeInTheDocument();
  });

  it("shows a countdown when the round is timed", () => {
    renderScreen(payload({ timeLimitSeconds: 30, timer: { deadlineAt: new Date(Date.now() + 20_000).toISOString(), serverNow: new Date().toISOString() } }));
    expect(screen.getByRole("timer", { name: /time left/i })).toBeInTheDocument();
  });
});
