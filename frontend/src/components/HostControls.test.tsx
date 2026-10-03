import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HostControls } from "./HostControls";

function renderControls(status: "Waiting" | "Active" | "Completed") {
  const handlers = { onStart: vi.fn(), onEnd: vi.fn(), onNewRound: vi.fn() };
  render(<HostControls status={status} busy={false} {...handlers} />);
  return handlers;
}

describe("HostControls", () => {
  it("starts a waiting game with one press", () => {
    const { onStart } = renderControls("Waiting");

    fireEvent.click(screen.getByRole("button", { name: "Start game" }));

    expect(onStart).toHaveBeenCalledTimes(1);
  });

  it("asks before ending a live game for everyone", () => {
    const { onEnd } = renderControls("Active");

    fireEvent.click(screen.getByRole("button", { name: "End game" }));
    expect(onEnd).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Yes, end the game" }));
    expect(onEnd).toHaveBeenCalledTimes(1);
  });

  it("says that a new game starts the scores again", () => {
    const { onNewRound } = renderControls("Completed");

    expect(screen.getByText(/scores start again from zero/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Start new game" }));

    expect(onNewRound).toHaveBeenCalledTimes(1);
  });
});
