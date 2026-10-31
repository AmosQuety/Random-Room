import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { HostControls } from "./HostControls";

function renderControls(status: "Waiting" | "Active" | "Completed", options: { hostPlays?: boolean; requiresReferee?: boolean } = {}) {
  const handlers = { onStart: vi.fn(), onEnd: vi.fn(), onNewRound: vi.fn(), onHostPlaysChange: vi.fn() };
  render(<HostControls status={status} busy={false} hostPlays={options.hostPlays ?? false} requiresReferee={options.requiresReferee ?? false} {...handlers} />);
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

  describe("play in the next game", () => {
    it("is offered only between games", () => {
      renderControls("Active");
      expect(screen.queryByRole("checkbox", { name: "Play in the next game" })).not.toBeInTheDocument();
    });

    it("is not offered before the first game", () => {
      renderControls("Waiting");
      expect(screen.queryByRole("checkbox", { name: "Play in the next game" })).not.toBeInTheDocument();
    });

    it("asks to play when ticked and explains what changes", () => {
      const { onHostPlaysChange } = renderControls("Completed");
      const toggle = screen.getByRole("checkbox", { name: "Play in the next game" });

      expect(toggle).not.toBeChecked();
      expect(toggle).toHaveAccessibleDescription(/appear on the scoreboard.*Spin the Wheel.*only when you are the judge/);
      fireEvent.click(toggle);

      expect(onHostPlaysChange).toHaveBeenCalledWith(true);
    });

    it("asks to referee again when unticked", () => {
      const { onHostPlaysChange } = renderControls("Completed", { hostPlays: true });

      fireEvent.click(screen.getByRole("checkbox", { name: "Play in the next game" }));

      expect(onHostPlaysChange).toHaveBeenCalledWith(false);
    });

    it("is disabled with the reason when the game always needs a referee", () => {
      renderControls("Completed", { requiresReferee: true });
      const toggle = screen.getByRole("checkbox", { name: "Play in the next game" });

      expect(toggle).toBeDisabled();
      expect(toggle).toHaveAccessibleDescription("In this game the host always judges, so the host cannot play.");
    });
  });
});
