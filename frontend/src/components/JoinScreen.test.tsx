import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { RoomPreview } from "../lib/types";
import { JoinScreen } from "./JoinScreen";

const api = vi.hoisted(() => ({ getRoomPreview: vi.fn(), join: vi.fn() }));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));

const preview = {
  title: "Friday night",
  gameType: "random-picker",
  players: [
    { name: "Amos", claimed: true },
    { name: "Lydia", claimed: true },
  ],
} as unknown as RoomPreview;

describe("JoinScreen", () => {
  beforeEach(() => api.getRoomPreview.mockReset().mockResolvedValue(preview));

  it("says what is still needed while the button is disabled, and updates as the player fills it in", async () => {
    render(<JoinScreen slug="abc123" onJoined={vi.fn()} />);

    const button = await screen.findByRole("button", { name: /enter the room/i });
    expect(button).toBeDisabled();
    expect(screen.getByText(/pick your name above/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("radio", { name: /amos/i }));
    expect(screen.getByText(/enter the pin you chose/i)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/your secret pin/i), { target: { value: "1234" } });
    expect(button).toBeEnabled();
    expect(screen.queryByText(/pick your name above/i)).not.toBeInTheDocument();
  });
});
