import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import type { Session } from "../lib/types";
import { ClaimInviteScreen } from "./ClaimInviteScreen";

const api = vi.hoisted(() => ({ claimInvite: vi.fn(), join: vi.fn() }));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));

const session: Session = { token: "t", player: "Amos", roomSlug: "abc123", isHost: false };

function fillAndSubmit(pin: string) {
  fireEvent.change(screen.getByLabelText("Your secret PIN"), { target: { value: pin } });
  fireEvent.change(screen.getByLabelText("Confirm PIN"), { target: { value: pin } });
  fireEvent.click(screen.getByRole("button", { name: /set my pin/i }));
}

describe("ClaimInviteScreen", () => {
  beforeEach(() => {
    api.claimInvite.mockReset().mockResolvedValue({ player: "Amos" });
    api.join.mockReset().mockResolvedValue(session);
  });

  it("signs the player straight in with the PIN they just set", async () => {
    const onJoined = vi.fn();
    render(<ClaimInviteScreen slug="abc123" token="tok" onJoined={onJoined} />);

    fillAndSubmit("1234");

    await waitFor(() => expect(onJoined).toHaveBeenCalledWith(session));
    expect(api.join).toHaveBeenCalledWith("abc123", "Amos", "1234");
    expect(window.location.pathname).toBe("/room/abc123");
  });

  it("falls back to the manual join step when automatic sign-in fails", async () => {
    api.join.mockRejectedValue(new Error("offline"));
    const onJoined = vi.fn();
    render(<ClaimInviteScreen slug="abc123" token="tok" onJoined={onJoined} />);

    fillAndSubmit("1234");

    expect(await screen.findByRole("button", { name: /continue to the room/i })).toBeInTheDocument();
    expect(onJoined).not.toHaveBeenCalled();
  });

  it("says so when the sign-in is throttled, instead of only showing success", async () => {
    api.join.mockRejectedValue(new ApiError("Too many attempts.", 429));
    render(<ClaimInviteScreen slug="abc123" token="tok" onJoined={vi.fn()} />);

    fillAndSubmit("1234");

    expect(await screen.findByRole("alert")).toHaveTextContent(/wait a minute/i);
  });
});
