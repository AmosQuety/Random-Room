import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../lib/types";
import { MadLibsGameScreen, type MadLibsPayload } from "./mad-libs/GameScreen";
import { RandomPickerGameScreen } from "./random-picker/GameScreen";
import type { RandomPickerPayload } from "./random-picker/types";
import { TriviaGameScreen } from "./trivia/GameScreen";
import type { TriviaPayload } from "./trivia/types";

const PLAYERS: PlayerView[] = ["Lydia", "James"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const referee = () => screen.queryByRole("region", { name: "You are refereeing" });

const trivia: TriviaPayload = {
  questionNumber: 1,
  totalQuestions: 2,
  currentQuestion: { text: "2 + 2?", options: ["3", "4"] },
  answered: { Lydia: false, James: false },
  lastReveal: null,
  scoreboard: [{ player: "Lydia", correct: 0 }, { player: "James", correct: 0 }],
  activity: [],
  timer: null,
  timeLimitSeconds: null,
};

describe("Trivia as the referee", () => {
  it("shows the question and its options but nothing to answer with", () => {
    render(<TriviaGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={trivia} busy={false} onAction={vi.fn()} />);

    expect(referee()).toBeInTheDocument();
    expect(screen.getByText("2 + 2?")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.getByText(/0\/2 answered/)).toBeInTheDocument();
  });

  it("still lets a player answer", () => {
    const onAction = vi.fn();
    render(<TriviaGameScreen me="Lydia" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={trivia} busy={false} onAction={onAction} />);

    fireEvent.click(screen.getByRole("button", { name: /4/ }));

    expect(referee()).not.toBeInTheDocument();
    expect(onAction).toHaveBeenCalledWith("answer", { optionIndex: 1 });
  });
});

describe("Random Picker as the referee", () => {
  const picker: RandomPickerPayload = {
    choices: ["Pizza", "Tacos"],
    players: { Lydia: { hasTriggered: false, result: null }, James: { hasTriggered: false, result: null } },
    tally: [],
    activity: [],
  };

  it("gives the referee no button, only the players' cards", () => {
    render(<RandomPickerGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={picker} busy={false} onAction={vi.fn()} />);

    expect(referee()).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("gives a player their button", () => {
    render(<RandomPickerGameScreen me="Lydia" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={picker} busy={false} onAction={vi.fn()} />);

    expect(screen.getAllByRole("button")).toHaveLength(1);
  });
});

describe("the shared round screen as the referee (Mad Libs)", () => {
  const madLibs: MadLibsPayload = {
    phase: "collecting",
    round: 1,
    totalRounds: 2,
    prompt: { labels: ["noun", "verb"], assigned: { Lydia: [0], James: [1] } },
    answered: { Lydia: false, James: false },
    myAnswer: null,
    result: null,
    scoreboard: [{ player: "Lydia", score: 0 }, { player: "James", score: 0 }],
    timer: { deadlineAt: null, serverNow: "2026-09-29T12:00:00Z" },
    timeLimitSeconds: null,
  };

  it("offers no word fields and keeps the reveal button", () => {
    const onAction = vi.fn();
    render(<MadLibsGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={madLibs} busy={false} onAction={onAction} />);

    expect(referee()).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /lock it in/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /reveal now/i }));
    expect(onAction).toHaveBeenCalledWith("reveal");
  });
});
