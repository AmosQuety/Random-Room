import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { ApiError } from "./lib/api";
import { loadSession } from "./lib/session";

const api = vi.hoisted(() => ({ getRoomPreview: vi.fn() }));
vi.mock("./lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));
vi.mock("./components/RoomScreen", () => ({
  RoomScreen: ({ onSessionExpired, onLeave }: { onSessionExpired: (reason: "expired" | "reset" | "deleted") => void; onLeave: () => void }) => (
    <>
      <button onClick={() => onSessionExpired("expired")}>expire</button>
      <button onClick={() => onSessionExpired("reset")}>reset</button>
      <button onClick={() => onSessionExpired("deleted")}>deleted</button>
      <button onClick={onLeave}>leave</button>
    </>
  ),
}));

const preview = { title: "Friday night", gameType: "random-picker", players: [{ name: "Amos", claimed: true }] };

describe("App sign-in expiry", () => {
  beforeEach(() => {
    api.getRoomPreview.mockReset().mockResolvedValue(preview);
    window.history.replaceState({}, "", "/room/abc");
    localStorage.setItem("random-room-session", JSON.stringify({ token: "t", player: "Amos", roomSlug: "abc", isHost: false }));
  });

  it("clears the stored session and says why when the sign-in expires", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "expire" }));

    expect(await screen.findByText(/your sign-in has ended/i)).toBeInTheDocument();
    expect(loadSession()).toBeNull();
  });

  it("tells a player whose seat the host reset to ask for a new invite link", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "reset" }));

    expect(await screen.findByText(/the host reset your seat/i)).toBeInTheDocument();
    expect(screen.getByText(/ask them for your new invite link/i)).toBeInTheDocument();
    expect(loadSession()).toBeNull();
  });

  it("tells a player the host deleted the room, on the page that now says it is gone", async () => {
    api.getRoomPreview.mockRejectedValue(new ApiError("Room not found.", 404));
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "deleted" }));

    expect(await screen.findByText(/the host deleted this room/i)).toBeInTheDocument();
    expect(loadSession()).toBeNull();
  });

  it("says nothing special when the player leaves on purpose", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "leave" }));

    await screen.findByRole("button", { name: /enter the room/i });
    expect(screen.queryByText(/your sign-in has ended/i)).not.toBeInTheDocument();
  });

  it("shows the host recovery page at /room/<slug>/recover, signed in or not", () => {
    window.history.replaceState({}, "", "/room/abc/recover");
    render(<App />);

    expect(screen.getByRole("heading", { name: /forgot your host pin/i })).toBeInTheDocument();
  });
});
