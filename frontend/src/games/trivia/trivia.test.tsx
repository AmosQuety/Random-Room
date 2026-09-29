import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../../lib/types";
import { TriviaGameScreen } from "./GameScreen";
import { TriviaSetupForm } from "./SetupForm";
import { defaultSetup, isSetupValid, toApiSetup } from "./setup";
import type { TriviaPayload, TriviaSetup } from "./types";

const q = (text: string, correctIndex = 0, category = "") => ({ text, options: ["a", "b"], correctIndex, category });

describe("trivia setup", () => {
  it("is not valid until there is a question or the starter set is on", () => {
    expect(isSetupValid(defaultSetup)).toBe(false);
    expect(isSetupValid({ ...defaultSetup, useBuiltIn: true })).toBe(true);
    expect(isSetupValid({ ...defaultSetup, questions: [q("What?")] })).toBe(true);
  });

  it("ignores untouched rows but rejects a half-written question", () => {
    expect(isSetupValid({ ...defaultSetup, useBuiltIn: true, questions: [q("What?"), { ...q(""), options: ["", ""] }] })).toBe(true);
    expect(isSetupValid({ ...defaultSetup, questions: [{ text: "What?", options: ["a", ""], correctIndex: 0, category: "" }] })).toBe(false);
  });

  it("sends categories, the starter choice and the time limit only when they are set", () => {
    const plain = toApiSetup({ ...defaultSetup, questions: [q("What?")] });
    expect(plain).toEqual({ questions: [{ text: "What?", options: ["a", "b"], correctIndex: 0 }] });

    const full = toApiSetup({ questions: [q(" What? ", 1, " Science ")], useBuiltIn: true, builtInCount: 5, timeLimit: 30 });
    expect(full).toEqual({
      questions: [{ text: "What?", options: ["a", "b"], correctIndex: 1, category: "Science" }],
      useBuiltIn: true,
      builtInCount: 5,
      timeLimitSeconds: 30,
    });
  });

  it("lets the host switch on the starter set and choose a time limit", () => {
    const onChange = vi.fn();
    render(<TriviaSetupForm value={defaultSetup as TriviaSetup} onChange={onChange} />);

    fireEvent.click(screen.getByRole("checkbox", { name: /starter set/i }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ useBuiltIn: true }));

    fireEvent.change(screen.getByLabelText("Time per question"), { target: { value: "30" } });
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ timeLimit: 30 }));
  });

  it("offers a category field on every question", () => {
    const onChange = vi.fn();
    render(<TriviaSetupForm value={defaultSetup as TriviaSetup} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Question 1 category"), { target: { value: "Science" } });

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ questions: [expect.objectContaining({ category: "Science" })] }));
  });
});

describe("trivia game screen", () => {
  const players: PlayerView[] = ["Amos", "Lydia"].map((name) => ({ name, online: true }));
  const session: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
  const payload: TriviaPayload = {
    questionNumber: 1,
    totalQuestions: 2,
    currentQuestion: { text: "2 + 2?", options: ["3", "4"], category: "Maths" },
    answered: { Amos: false, Lydia: false },
    lastReveal: null,
    scoreboard: [{ player: "Amos", correct: 0 }, { player: "Lydia", correct: 0 }],
    activity: [],
    timer: { deadlineAt: new Date(Date.now() + 20_000).toISOString(), serverNow: new Date().toISOString() },
    timeLimitSeconds: 20,
  };

  it("shows the category and a countdown when the question is timed", () => {
    render(<TriviaGameScreen me="Amos" isHost={false} players={players} session={session} payload={payload} busy={false} onAction={vi.fn()} />);

    expect(screen.getByText("Maths")).toBeInTheDocument();
    expect(screen.getByRole("timer", { name: /time left/i })).toBeInTheDocument();
  });

  it("shows neither when the game is untimed and uncategorised", () => {
    const plain = { ...payload, timer: null, timeLimitSeconds: null, currentQuestion: { text: "2 + 2?", options: ["3", "4"] } };
    render(<TriviaGameScreen me="Amos" isHost={false} players={players} session={session} payload={plain} busy={false} onAction={vi.fn()} />);

    expect(screen.queryByRole("timer")).not.toBeInTheDocument();
  });
});
