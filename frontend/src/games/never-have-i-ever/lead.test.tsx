import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { NeverHaveIEverGameScreen, type NeverHaveIEverPayload } from "./GameScreen";
import { withoutLead } from "./lead";

describe("withoutLead", () => {
  it.each([
    ["Never have I ever eaten breakfast for dinner", "eaten breakfast for dinner"],
    ["never have i ever, been on a boat", "been on a boat"],
    ["Never have I ever... sung in the car", "sung in the car"],
  ])("drops the lead-in from %j", (text, expected) => {
    expect(withoutLead(text)).toBe(expected);
  });

  it("leaves a statement alone when it does not start with the lead-in", () => {
    expect(withoutLead("eaten breakfast for dinner")).toBe("eaten breakfast for dinner");
  });

  it("never returns an empty statement", () => {
    expect(withoutLead("Never have I ever")).toBe("Never have I ever");
  });
});

describe("Never Have I Ever screen", () => {
  it("says the lead-in once, however the statement was written", () => {
    const players: PlayerView[] = [{ name: "Amos", online: true, claimed: true }];
    const session: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
    const payload = {
      phase: "collecting",
      round: 1,
      totalRounds: 3,
      prompt: { text: "Never have I ever been on a boat" },
      answered: { Amos: false },
      myAnswer: null,
      result: null,
      scoreboard: [{ player: "Amos", score: 0 }],
      timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
      timeLimitSeconds: null,
    } as unknown as NeverHaveIEverPayload;

    render(<NeverHaveIEverGameScreen me="Amos" isHost={false} players={players} session={session} payload={payload} busy={false} onAction={vi.fn()} />);

    expect(screen.getByText("Never have I ever...")).toBeInTheDocument();
    expect(screen.getByText("been on a boat")).toBeInTheDocument();
    expect(screen.queryByText(/never have i ever been on a boat/i)).not.toBeInTheDocument();
  });
});
