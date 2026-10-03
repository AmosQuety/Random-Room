import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { loadSession } from "./lib/session";

const api = vi.hoisted(() => ({ getRoomPreview: vi.fn() }));
vi.mock("./lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));
vi.mock("./components/RoomScreen", () => ({
  RoomScreen: ({ onSessionExpired, onLeave }: { onSessionExpired: () => void; onLeave: () => void }) => (
    <>
      <button onClick={onSessionExpired}>expire</button>
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

  it("says nothing special when the player leaves on purpose", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "leave" }));

    await screen.findByRole("button", { name: /enter the room/i });
    expect(screen.queryByText(/your sign-in has ended/i)).not.toBeInTheDocument();
  });
});
