import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlayerView, SessionView } from "../lib/types";
import { ChainGameScreen } from "./story-chain/GameScreen";
import type { ChainConfig, ChainPayload } from "./story-chain/types";
import { GuessWhoGameScreen } from "./guess-who/GameScreen";
import type { GuessWhoPayload } from "./guess-who/types";
import { TwoTruthsGameScreen } from "./two-truths/GameScreen";
import type { TwoTruthsPayload } from "./two-truths/types";

const PLAYERS: PlayerView[] = ["Lydia", "James"].map((name) => ({ name, online: true, claimed: true }));
const ACTIVE: SessionView = { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null };
const refereeNote = () => screen.queryByRole("region", { name: "You are refereeing" });
const scores = [{ player: "Lydia", score: 0 }, { player: "James", score: 0 }];

describe("Two Truths as the referee", () => {
  const twoTruths = (overrides: Partial<TwoTruthsPayload>): TwoTruthsPayload =>
    ({ phase: "voting", round: 1, totalRounds: 2, storyteller: "Lydia", statements: ["A", "B", "C"], myLie: null, myVote: null, voted: { James: false }, result: null, scoreboard: scores, ...overrides }) as TwoTruthsPayload;

  it("sees the statements but cannot vote, and can reveal", () => {
    const onAction = vi.fn();
    render(<TwoTruthsGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={twoTruths({})} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.getByText("B")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^3/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /reveal now/i }));
    expect(onAction).toHaveBeenCalledWith("reveal");
  });

  it("still lets a player vote", () => {
    const onAction = vi.fn();
    render(<TwoTruthsGameScreen me="James" isHost={false} referee={false} players={PLAYERS} session={ACTIVE} payload={twoTruths({})} busy={false} onAction={onAction} />);

    fireEvent.click(screen.getByRole("button", { name: /B/ }));

    expect(onAction).toHaveBeenCalledWith("vote", { choice: 1 });
  });
});

describe("Guess Who as the referee", () => {
  const guessWho = (overrides: Partial<GuessWhoPayload>): GuessWhoPayload =>
    ({ phase: "submitting", round: 0, totalRounds: 0, text: null, isMine: false, myFact: null, myGuess: null, submitted: { Lydia: true, James: false }, guessCount: 0, guessesNeeded: 2, result: null, scoreboard: scores, ...overrides }) as GuessWhoPayload;

  it("has no fact form while facts are written, and can begin", () => {
    const onAction = vi.fn();
    render(<GuessWhoGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={guessWho({ submitted: { Lydia: true, James: true } })} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /begin with 2 facts/i }));
    expect(onAction).toHaveBeenCalledWith("begin");
  });

  it("has no guess buttons while a fact is up", () => {
    render(<GuessWhoGameScreen me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={guessWho({ phase: "voting", round: 1, totalRounds: 2, text: "I ran a marathon" })} busy={false} onAction={vi.fn()} />);

    expect(screen.queryByRole("button", { name: /Lydia|James/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /reveal now/i })).toBeInTheDocument();
  });
});

describe("a story game as the referee", () => {
  const config = { piece: "word", inputHint: "Add one word", maxChars: 20, singleWord: true } as ChainConfig;
  const chain = { phase: "writing", turn: 0, totalTurns: 4, opener: "Once", entries: [], currentPlayer: "Lydia", prefix: null, scoreboard: scores } as unknown as ChainPayload;

  it("has no writing form and can skip or end", () => {
    const onAction = vi.fn();
    render(<ChainGameScreen config={config} me="Amos" isHost referee players={PLAYERS} session={ACTIVE} payload={chain} busy={false} onAction={onAction} />);

    expect(refereeNote()).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /skip lydia/i }));
    expect(onAction).toHaveBeenCalledWith("skip");
  });
});
