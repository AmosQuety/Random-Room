import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ResultBar } from "./ResultBar";

const renderBar = (props: Partial<Parameters<typeof ResultBar>[0]>) =>
  render(
    <ul>
      <ResultBar label="Pizza" count={3} total={4} unit="vote" {...props} />
    </ul>,
  );

describe("ResultBar", () => {
  it("shows the share of people by default, as polls need", () => {
    renderBar({});
    expect(screen.getByText("3 votes · 75%")).toBeInTheDocument();
  });

  it("shows only the count when the bar is relative to the best answer, since that is not a share of anyone", () => {
    renderBar({ unit: "point", count: 26, total: 30, measure: "relative" });
    expect(screen.getByText("26 points")).toBeInTheDocument();
    expect(screen.queryByText(/%/)).not.toBeInTheDocument();
  });
});
