import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { RoomSnapshot } from "../lib/types";
import { FinalResult } from "./FinalResult";

const snapshot = (sarah: number, judith: number): RoomSnapshot =>
  ({
    round: { number: 3 },
    tally: [
      { choice: "Sarah", count: sarah },
      { choice: "Judith", count: judith },
    ],
  }) as RoomSnapshot;

describe("FinalResult", () => {
  it("names the choice picked most often", () => {
    render(<FinalResult snapshot={snapshot(1, 3)} />);
    expect(screen.getByText(/judith takes it/i)).toBeInTheDocument();
  });

  it("reports a tie", () => {
    render(<FinalResult snapshot={snapshot(2, 2)} />);
    expect(screen.getByText(/it's a tie/i)).toBeInTheDocument();
  });

  it("handles a round ended before anyone rolled", () => {
    render(<FinalResult snapshot={snapshot(0, 0)} />);
    expect(screen.getByText(/no rolls this round/i)).toBeInTheDocument();
  });
});
