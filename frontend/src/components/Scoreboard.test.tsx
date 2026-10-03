import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { rankRows } from "../lib/ranking";
import { Scoreboard } from "./Scoreboard";

describe("rankRows", () => {
  it("shares a rank between equal scores and skips the next", () => {
    const ranked = rankRows([
      { name: "A", score: 3 },
      { name: "B", score: 5 },
      { name: "C", score: 3 },
      { name: "D", score: 1 },
    ]);
    expect(ranked.map((r) => [r.row.name, r.rank])).toEqual([
      ["B", 1],
      ["A", 2],
      ["C", 2],
      ["D", 4],
    ]);
  });
});

describe("Scoreboard", () => {
  const rows = [
    { name: "Amos", score: 2 },
    { name: "Lydia", score: 4 },
  ];

  it("lists players best-first and marks me", () => {
    render(<Scoreboard rows={rows} me="Amos" unit="correct" />);
    const items = within(screen.getByRole("list")).getAllByRole("listitem");
    expect(items[0]).toHaveTextContent("Lydia");
    expect(items[1]).toHaveTextContent("Amos");
    expect(items[1]).toHaveTextContent("(you)");
  });

  it("gives the score a spoken unit, not just a number", () => {
    render(<Scoreboard rows={rows} me="Amos" unit="correct" />);
    const items = within(screen.getByRole("list")).getAllByRole("listitem");
    expect(items[0]).toHaveTextContent("4 correct");
    expect(items[1]).toHaveTextContent("2 correct");
  });

  it("says so when nobody has scored", () => {
    render(<Scoreboard rows={[]} me="Amos" />);
    expect(screen.getByText(/nobody has scored yet/i)).toBeInTheDocument();
  });

  it("does not crown a leader while everyone is on zero", () => {
    render(<Scoreboard rows={[{ name: "Amos", score: 0 }]} me="Amos" />);
    expect(screen.queryByRole("img", { name: /leading/i })).toBeNull();
  });

  it("does not rank anyone while everyone is on zero, so nobody looks like they are winning", () => {
    render(
      <Scoreboard
        rows={[
          { name: "Amos", score: 0 },
          { name: "Lydia", score: 0 },
        ]}
        me="Amos"
      />,
    );

    const items = within(screen.getByRole("list")).getAllByRole("listitem");
    for (const item of items) {
      expect(item).toHaveTextContent("Not ranked yet");
      expect(item).not.toHaveTextContent(/rank 1/i);
    }
  });

  it("ranks players as soon as anyone scores", () => {
    render(<Scoreboard rows={[{ name: "Amos", score: 1 }, { name: "Lydia", score: 0 }]} me="Amos" />);

    expect(screen.getAllByText(/^Rank/)).toHaveLength(2);
    expect(screen.queryByText(/not ranked yet/i)).not.toBeInTheDocument();
  });
});
