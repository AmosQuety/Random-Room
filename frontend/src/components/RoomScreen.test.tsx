import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import type { RoomSnapshot, Session } from "../lib/types";
import { RoomScreen } from "./RoomScreen";

const mocks = vi.hoisted(() => ({ snapshot: null as unknown, performAction: vi.fn() }));

vi.mock("../lib/useRoom", () => ({
  useRoom: () => ({ snapshot: mocks.snapshot, connection: "live", loadFailed: false, retry: vi.fn(), applySnapshot: vi.fn() }),
}));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), performAction: mocks.performAction }));
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
    const { rerender } = render(<RoomScreen session={session} onLeave={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "answer" }));
    expect(await screen.findByText("Answers are closed for this round.")).toBeInTheDocument();

    show(11);
    rerender(<RoomScreen session={session} onLeave={vi.fn()} />);

    expect(screen.queryByText("Answers are closed for this round.")).not.toBeInTheDocument();
  });

  it("does not show an error, or lock the screen, when a background timer tick fails", async () => {
    mocks.performAction.mockRejectedValue(new ApiError("The session is not active.", 409));
    render(<RoomScreen session={session} onLeave={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "tick" }));

    expect(screen.getByText("idle")).toBeInTheDocument();
    await waitFor(() => expect(mocks.performAction).toHaveBeenCalledWith("t", "tick", undefined));
    await act(async () => {});
    expect(screen.queryByText("The session is not active.")).not.toBeInTheDocument();
  });

  it("returns to the join screen, with no error, when an action finds the sign-in has expired", async () => {
    mocks.performAction.mockRejectedValue(new ApiError("Unauthorized", 401));
    const onLeave = vi.fn();
    render(<RoomScreen session={session} onLeave={onLeave} />);

    fireEvent.click(screen.getByRole("button", { name: "answer" }));

    await waitFor(() => expect(onLeave).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
