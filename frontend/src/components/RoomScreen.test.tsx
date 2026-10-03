import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import type { RoomSnapshot, Session } from "../lib/types";
import { RoomScreen } from "./RoomScreen";

const mocks = vi.hoisted(() => ({ snapshot: null as unknown, performAction: vi.fn(), deleteRoom: vi.fn(), onUnauthorized: null as null | (() => void) }));

vi.mock("../lib/useRoom", () => ({
  useRoom: (_token: string, onUnauthorized: () => void) => {
    mocks.onUnauthorized = onUnauthorized;
    return { snapshot: mocks.snapshot, connection: "live", loadFailed: false, retry: vi.fn(), applySnapshot: vi.fn() };
  },
}));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), performAction: mocks.performAction, deleteRoom: mocks.deleteRoom }));
vi.mock("../games/registry", () => ({
  GAMES: {
    fake: {
      name: "Fake game",
      accent: "sky",
      Glyph: () => null,
      GameScreen: ({ onAction, busy }: { onAction: (a: string) => void; busy: boolean }) => (
        <>
          <button onClick={() => onAction("answer")}>answer</button>
          <button onClick={() => onAction("tick")}>tick</button>
          <span>{busy ? "busy" : "idle"}</span>
        </>
      ),
    },
  },
}));

const session: Session = { token: "t", player: "Amos", roomSlug: "abc", isHost: false };
const hostSession: Session = { ...session, isHost: true };

const snapshot = (sequence: number) =>
  ({
    roomId: "r1",
    roomSlug: "abc",
    roomTitle: "Room",
    hostPlayer: "Lydia",
    gameType: "fake",
    session: { id: "s", number: 1, status: "Active", startedAt: null, endedAt: null },
    players: [],
    gamePayload: {},
    sequence,
  }) as RoomSnapshot;

function show(sequence: number) {
  mocks.snapshot = snapshot(sequence);
}

describe("RoomScreen errors", () => {
  beforeEach(() => {
    mocks.performAction.mockReset();
    show(10);
  });

  it("clears a failed action's message once a newer snapshot arrives", async () => {
    mocks.performAction.mockRejectedValue(new ApiError("Answers are closed for this round.", 409));
    const { rerender } = render(<RoomScreen session={session} onLeave={vi.fn()} onSessionExpired={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "answer" }));
    expect(await screen.findByText("Answers are closed for this round.")).toBeInTheDocument();

    show(11);
    rerender(<RoomScreen session={session} onLeave={vi.fn()} onSessionExpired={vi.fn()} />);

    expect(screen.queryByText("Answers are closed for this round.")).not.toBeInTheDocument();
  });

  it("does not show an error, or lock the screen, when a background timer tick fails", async () => {
    mocks.performAction.mockRejectedValue(new ApiError("The session is not active.", 409));
    render(<RoomScreen session={session} onLeave={vi.fn()} onSessionExpired={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "tick" }));

    expect(screen.getByText("idle")).toBeInTheDocument();
    await waitFor(() => expect(mocks.performAction).toHaveBeenCalledWith("t", "tick", undefined));
    await act(async () => {});
    expect(screen.queryByText("The session is not active.")).not.toBeInTheDocument();
  });

  it("returns to the join screen, with no error, when an action finds the sign-in has expired", async () => {
    mocks.performAction.mockRejectedValue(new ApiError("Unauthorized", 401));
    const onLeave = vi.fn();
    const onSessionExpired = vi.fn();
    render(<RoomScreen session={session} onLeave={onLeave} onSessionExpired={onSessionExpired} />);

    fireEvent.click(screen.getByRole("button", { name: "answer" }));

    await waitFor(() => expect(onSessionExpired).toHaveBeenCalledTimes(1));
    expect(onLeave).not.toHaveBeenCalled();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("treats a 401 while loading the room as an expired sign-in too", () => {
    const onSessionExpired = vi.fn();
    render(<RoomScreen session={session} onLeave={vi.fn()} onSessionExpired={onSessionExpired} />);

    mocks.onUnauthorized?.();

    expect(onSessionExpired).toHaveBeenCalledTimes(1);
  });

  it("gives only the host a way to delete the room, and signs everyone out with the right reason when it happens", async () => {
    mocks.deleteRoom.mockResolvedValue(undefined);
    const onSessionExpired = vi.fn();
    const { unmount } = render(<RoomScreen session={session} onLeave={vi.fn()} onSessionExpired={onSessionExpired} />);
    expect(screen.queryByRole("button", { name: "Delete room" })).not.toBeInTheDocument();
    unmount();

    render(<RoomScreen session={hostSession} onLeave={vi.fn()} onSessionExpired={onSessionExpired} />);
    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));
    fireEvent.click(screen.getByRole("button", { name: "Yes, delete the room" }));

    await waitFor(() => expect(mocks.deleteRoom).toHaveBeenCalledWith("t"));
    await waitFor(() => expect(onSessionExpired).toHaveBeenCalledWith("deleted"));
  });

  it("stays in the room and says why when deleting fails", async () => {
    mocks.deleteRoom.mockRejectedValue(new ApiError("Something went wrong.", 500));
    const onSessionExpired = vi.fn();
    render(<RoomScreen session={hostSession} onLeave={vi.fn()} onSessionExpired={onSessionExpired} />);

    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));
    fireEvent.click(screen.getByRole("button", { name: "Yes, delete the room" }));

    expect(await screen.findByText("Something went wrong.")).toBeInTheDocument();
    expect(onSessionExpired).not.toHaveBeenCalled();
  });
});
