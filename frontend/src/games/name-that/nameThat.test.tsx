import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { NameThatGameScreen, type NameThatPayload } from "./GameScreen";
import { isHttpsLink, kit } from "./kit";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const base = {
  phase: "collecting" as const,
  round: 1,
  totalRounds: 1,
  answered: { Amos: false, Lydia: false },
  myAnswer: null,
  result: null,
  scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
  timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
  timeLimitSeconds: null,
};
const props = { me: "Amos", isHost: false, players: PLAYERS, session: ACTIVE, busy: false };

describe("Name That screen", () => {
  it("opens a clip link in a new tab without giving the page to the link", () => {
    const payload: NameThatPayload = { ...base, prompt: { clue: "Yellow boat", link: "https://example.com/clip" } };
    render(<NameThatGameScreen {...props} payload={payload} onAction={vi.fn()} />);

    const link = screen.getByRole("link", { name: /open the clip/i });
    expect(link).toHaveAttribute("href", "https://example.com/clip");
    expect(link).toHaveAttribute("target", "_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
    expect(link.getAttribute("rel")).toContain("noreferrer");
    expect(document.querySelector("iframe")).toBeNull();
  });

  it("never renders a link that is not https, even if the server sent one", () => {
    const payload: NameThatPayload = { ...base, prompt: { clue: "x", link: "javascript:alert(1)" } };
    render(<NameThatGameScreen {...props} payload={payload} onAction={vi.fn()} />);

    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("sends a trimmed guess and will not send an empty one", () => {
    const onAction = vi.fn();
    render(<NameThatGameScreen {...props} payload={{ ...base, prompt: { clue: "Yellow boat", link: null } }} onAction={onAction} />);

    const lockIn = screen.getByRole("button", { name: /lock it in/i });
    expect(lockIn).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Your guess"), { target: { value: "  Yellow Submarine " } });
    fireEvent.click(lockIn);

    expect(onAction).toHaveBeenCalledWith("answer", { text: "Yellow Submarine" });
  });

  it("shows the answer, who got it, and the first-correct bonus at the reveal", () => {
    const payload: NameThatPayload = {
      ...base,
      phase: "revealed",
      prompt: { clue: "Yellow boat", link: null },
      result: {
        clue: "Yellow boat",
        accepted: ["Yellow Submarine", "The Yellow Submarine"],
        first: "Lydia",
        results: [{ player: "Lydia", text: "yellow submarine", correct: true }, { player: "Amos", text: "Help", correct: false }],
      },
    };
    render(<NameThatGameScreen {...props} payload={payload} onAction={vi.fn()} />);

    expect(screen.getByText(/^Answer:/)).toHaveTextContent("Answer: Yellow Submarine");
    expect(screen.getByText("Correct, first (+2)").closest("li")).toHaveTextContent("Lydia");
    expect(screen.getByText("Wrong").closest("li")).toHaveTextContent("Amos");
  });
});

describe("clue setup", () => {
  const clue = (over = {}) => ({ clue: "Yellow boat", link: "", answers: ["Yellow Submarine", ""], ...over });

  it("accepts only full https links without credentials", () => {
    expect(isHttpsLink("https://example.com/a")).toBe(true);
    for (const bad of ["http://example.com", "javascript:alert(1)", "example.com", "https://u:p@example.com", "", "//example.com"]) expect(isHttpsLink(bad)).toBe(false);
  });

  it("needs a clue and at least one accepted answer, and a valid link if there is one", () => {
    expect(kit.isValid(clue())).toBe(true);
    expect(kit.isValid(clue({ clue: "" }))).toBe(false);
    expect(kit.isValid(clue({ answers: ["", ""] }))).toBe(false);
    expect(kit.isValid(clue({ link: "http://example.com" }))).toBe(false);
    expect(kit.isValid(clue({ link: "https://example.com/x" }))).toBe(true);
  });

  it("sends only filled answers and omits an empty link", () => {
    expect(kit.toApi(clue({ answers: [" A ", "", "B"] }))).toEqual({ clue: "Yellow boat", answers: ["A", "B"] });
    expect(kit.toApi(clue({ link: " https://example.com/x " }))).toMatchObject({ link: "https://example.com/x" });
  });
});
