import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { MadLibsGameScreen, type MadLibsPayload } from "./GameScreen";
import { blankLabels, kit, storyProblem } from "./kit";

const PLAYERS: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };

function payload(overrides: Partial<MadLibsPayload> = {}): MadLibsPayload {
  return {
    phase: "collecting",
    round: 1,
    totalRounds: 1,
    prompt: { labels: ["adjective", "animal", "place"], assigned: { Amos: [0, 2], Lydia: [1] } },
    answered: { Amos: false, Lydia: false },
    myAnswer: null,
    result: null,
    scoreboard: PLAYERS.map((p) => ({ player: p.name, score: 0 })),
    timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
    timeLimitSeconds: null,
    ...overrides,
  };
}

function renderScreen(p: MadLibsPayload, me = "Amos") {
  const onAction = vi.fn();
  render(<MadLibsGameScreen me={me} isHost={false} players={PLAYERS} session={ACTIVE} payload={p} busy={false} onAction={onAction} />);
  return onAction;
}

describe("Fill-in Stories screen", () => {
  it("asks each player only for their own blanks and sends the trimmed words in order", () => {
    const onAction = renderScreen(payload());
    expect(screen.queryByLabelText("animal")).not.toBeInTheDocument();
    const lockIn = screen.getByRole("button", { name: /lock it in/i });
    expect(lockIn).toBeDisabled();

    fireEvent.change(screen.getByLabelText("adjective"), { target: { value: " purple " } });
    fireEvent.change(screen.getByLabelText("place"), { target: { value: "moon" } });
    fireEvent.click(lockIn);

    expect(onAction).toHaveBeenCalledWith("answer", { words: ["purple", "moon"] });
  });

  it("never shows a story before the reveal", () => {
    renderScreen(payload());
    expect(screen.getByText(/a story is waiting/i)).toBeInTheDocument();
    expect(screen.getByText(/3 gaps/i)).toBeInTheDocument();
  });

  it("shows the finished story with the words marked, as plain text", () => {
    renderScreen(
      payload({
        phase: "revealed",
        myAnswer: { words: ["<b>x</b>", "moon"] },
        result: {
          title: "Moon trip",
          parts: [{ text: "A " }, { word: "<b>x</b>", by: "Amos", label: "adjective" }, { text: " cat went to the " }, { word: "moon", by: "Amos", label: "place" }, { text: "." }],
        },
      }),
    );
    expect(screen.getByText("Moon trip")).toBeInTheDocument();
    expect(screen.getByText("<b>x</b>")).toBeInTheDocument();
    expect(document.querySelector("mark b")).toBeNull();
    expect(screen.getByText("moon").tagName).toBe("MARK");
  });
});

describe("Fill-in Stories setup", () => {
  it("finds blanks the way the server does", () => {
    expect(blankLabels("A {noun} and a { plural noun } and {}")).toEqual(["noun", "plural noun", ""]);
  });

  it("explains what is wrong with a story", () => {
    expect(storyProblem({ title: "", text: "{a}{b}" })).toMatch(/title/i);
    expect(storyProblem({ title: "T", text: "one {noun}" })).toMatch(/2 to 12/);
    expect(storyProblem({ title: "T", text: "{a} {}" })).toMatch(/word type/);
    expect(storyProblem({ title: "T", text: "{a} and {b}" })).toBeNull();
  });

  it("sends trimmed stories and ignores untouched rows", () => {
    expect(kit.isFilled(kit.empty())).toBe(false);
    expect(kit.toApi({ title: " T ", text: " {a} {b} " })).toEqual({ title: "T", text: "{a} {b}" });
    expect(kit.builtInCount).toBe(30);
  });
});
