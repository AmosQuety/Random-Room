import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { TwoTruthsGameScreen } from "./GameScreen";
import type { TwoTruthsPayload } from "./types";

const PLAYERS: PlayerView[] = ["Amos", "Lydia", "James"].map((name) => ({ name, online: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const STATEMENTS = ["I met a president", "I can juggle", "I ate a whole pie"];

function payload(overrides: Partial<TwoTruthsPayload> = {}): TwoTruthsPayload {
  return {
    phase: "voting",
    round: 1,
    totalRounds: 3,
    storyteller: "Lydia",
    statements: STATEMENTS,
    voted: { Amos: false, James: false },
    myVote: null,
    myLie: null,
    result: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    ...overrides,
  };
}

function renderScreen(p: TwoTruthsPayload, me = "Amos", isHost = false) {
  const onAction = vi.fn();
  render(<TwoTruthsGameScreen me={me} isHost={isHost} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Two Truths and a Lie screen", () => {
  it("shows the storyteller a form and nothing is sent until they submit", () => {
    const onAction = renderScreen(payload({ phase: "submitting", statements: null }), "Lydia");
    const lockIn = screen.getByRole("button", { name: /lock them in/i });
    expect(lockIn).toBeDisabled();

    ["One", "Two", "Three"].forEach((text, i) => fireEvent.change(screen.getByLabelText(`Statement ${i + 1}`), { target: { value: text } }));
    expect(lockIn).toBeDisabled();
    fireEvent.click(screen.getAllByRole("radio")[2]);
    fireEvent.click(lockIn);

    expect(onAction).toHaveBeenCalledWith("submit", { statements: ["One", "Two", "Three"], lie: 2 });
  });

  it("refuses duplicate statements", () => {
    renderScreen(payload({ phase: "submitting", statements: null }), "Lydia");
    ["Same", "same", "Other"].forEach((text, i) => fireEvent.change(screen.getByLabelText(`Statement ${i + 1}`), { target: { value: text } }));
    fireEvent.click(screen.getAllByRole("radio")[0]);

    expect(screen.getByRole("button", { name: /lock them in/i })).toBeDisabled();
    expect(screen.getByText(/must be different/i)).toBeInTheDocument();
  });

  it("tells the others whose turn it is while they wait", () => {
    renderScreen(payload({ phase: "submitting", statements: null }));
    expect(screen.getByText(/waiting for lydia to write/i)).toBeInTheDocument();
  });

  it("lets a voter pick the lie, once", () => {
    const onAction = renderScreen(payload());
    fireEvent.click(screen.getByRole("button", { name: /i can juggle/i }));
    expect(onAction).toHaveBeenCalledWith("vote", { choice: 1 });
  });

  it("locks the vote in and disables the buttons afterwards", () => {
    renderScreen(payload({ myVote: 1 }));
    expect(screen.getByText(/vote locked in/i)).toBeInTheDocument();
    for (const button of screen.getAllByRole("button", { name: /met a president|juggle|whole pie/i })) expect(button).toBeDisabled();
  });

  it("shows only the storyteller which statement is their lie, and gives them no voting buttons", () => {
    renderScreen(payload({ myLie: 0 }), "Lydia");
    expect(screen.getByText("Your lie")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /juggle/i })).not.toBeInTheDocument();
  });

  it("does not reveal the lie to a voter while voting", () => {
    renderScreen(payload());
    expect(screen.queryByText("The lie")).not.toBeInTheDocument();
    expect(screen.queryByText("Your lie")).not.toBeInTheDocument();
  });

  it("reveals the lie, the votes and who was fooled", () => {
    renderScreen(
      payload({
        phase: "revealed",
        result: { storyteller: "Lydia", statements: STATEMENTS, lie: 1, votes: [["James"], ["Amos"], []], caught: ["Amos"], fooled: ["James"] },
      }),
    );
    expect(screen.getByText("I can juggle").closest("li")).toHaveTextContent("The lie");
    expect(screen.getByText("I can juggle").closest("li")).toHaveTextContent("Voted lie: Amos");
    expect(screen.getByText(/fooled: james/i)).toBeInTheDocument();
    expect(screen.getByText(/caught it: amos/i)).toBeInTheDocument();
  });

  it("gives the host skip, reveal and next controls at the right moments, and no one else", () => {
    const skip = renderScreen(payload({ phase: "submitting", statements: null }), "Amos", true);
    fireEvent.click(screen.getByRole("button", { name: /skip lydia/i }));
    expect(skip).toHaveBeenCalledWith("skip");
  });

  it("hides host controls from regular players", () => {
    renderScreen(payload());
    expect(screen.queryByRole("button", { name: /reveal now/i })).not.toBeInTheDocument();
  });

  it("offers 'Finish game' after the last storyteller", () => {
    renderScreen(payload({ phase: "revealed", round: 3, result: { storyteller: "Lydia", statements: STATEMENTS, lie: 0, votes: [[], [], []], caught: [], fooled: [] } }), "Amos", true);
    expect(screen.getByRole("button", { name: /finish game/i })).toBeInTheDocument();
  });
});
