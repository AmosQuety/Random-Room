import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FinalResult } from "./FinalResult";

const tally = (sarah: number, judith: number) => [
  { choice: "Sarah", count: sarah },
  { choice: "Judith", count: judith },
];

describe("FinalResult", () => {
  it("names the choice picked most often", () => {
    render(<FinalResult roundNumber={3} tally={tally(1, 3)} />);
    expect(screen.getByText(/judith takes it/i)).toBeInTheDocument();
  });

  it("reports a tie", () => {
    render(<FinalResult roundNumber={3} tally={tally(2, 2)} />);
    expect(screen.getByText(/it's a tie/i)).toBeInTheDocument();
  });

  it("handles a round ended before anyone rolled", () => {
    render(<FinalResult roundNumber={3} tally={tally(0, 0)} />);
    expect(screen.getByText(/no rolls this round/i)).toBeInTheDocument();
  });
});
