import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PlayerCard, type RandomPickerPlayer } from "./PlayerCard";

const waiting: RandomPickerPlayer = { name: "Amos", online: true, hasTriggered: false, result: null };

function renderCard(overrides: Partial<Parameters<typeof PlayerCard>[0]> = {}) {
  const onTrigger = vi.fn();
  render(
    <ul>
      <PlayerCard player={waiting} isMe canTrigger rolling={false} onTrigger={onTrigger} {...overrides} />
    </ul>,
  );
  return onTrigger;
}

describe("PlayerCard", () => {
  it("lets me trigger the randomizer on my own card", () => {
    const onTrigger = renderCard();
    fireEvent.click(screen.getByRole("button", { name: /let randomness decide/i }));
    expect(onTrigger).toHaveBeenCalledOnce();
  });

  it("offers no button on someone else's card", () => {
    renderCard({ isMe: false, player: { ...waiting, name: "Lydia" } });
    expect(screen.queryByRole("button")).toBeNull();
    expect(screen.getByText(/waiting for lydia/i)).toBeInTheDocument();
  });

  it("disables the button while the round is not active", () => {
    renderCard({ canTrigger: false });
    expect(screen.getByRole("button")).toBeDisabled();
  });

  it("disables the button and shows progress while randomizing", () => {
    renderCard({ rolling: true });
    expect(screen.getByRole("button", { name: /randomizing/i })).toBeDisabled();
  });

  it("shows the result as a system result, never as a vote", () => {
    renderCard({ player: { ...waiting, hasTriggered: true, result: "Judith" } });
    expect(screen.getByText(/system random result/i)).toBeInTheDocument();
    expect(screen.getByText("Judith")).toBeInTheDocument();
    expect(screen.queryByRole("button")).toBeNull();
    expect(screen.queryByText(/vote/i)).toBeNull();
  });
});
