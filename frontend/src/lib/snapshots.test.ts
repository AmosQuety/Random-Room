import { describe, expect, it } from "vitest";
import { currentError, newerSnapshot } from "./snapshots";
import type { RoomSnapshot } from "./types";

const snapshot = (sequence: number, roomId = "room-1") => ({ roomId, sequence }) as RoomSnapshot;

describe("newerSnapshot", () => {
  it("takes the first snapshot it is given", () => {
    const first = snapshot(5);
    expect(newerSnapshot(null, first)).toBe(first);
  });

  it("takes a snapshot with a higher sequence", () => {
    const next = snapshot(6);
    expect(newerSnapshot(snapshot(5), next)).toBe(next);
  });

  it("keeps the current snapshot when a late, older one arrives", () => {
    const current = snapshot(9);
    expect(newerSnapshot(current, snapshot(7))).toBe(current);
  });

  it("takes an equal sequence, so a refetch of unchanged state still renders", () => {
    const next = snapshot(5);
    expect(newerSnapshot(snapshot(5), next)).toBe(next);
  });

  it("does not compare sequences across different rooms", () => {
    const other = snapshot(1, "room-2");
    expect(newerSnapshot(snapshot(900, "room-1"), other)).toBe(other);
  });
});

describe("currentError", () => {
  const error = { message: "Answers are closed.", sequence: 10 };

  it("shows the message while the room is still as it was when the action failed", () => {
    expect(currentError(error, snapshot(10))).toBe("Answers are closed.");
  });

  it("drops the message once a newer snapshot arrives", () => {
    expect(currentError(error, snapshot(11))).toBeNull();
  });

  it("shows nothing when there is no error", () => {
    expect(currentError(null, snapshot(10))).toBeNull();
  });
});
