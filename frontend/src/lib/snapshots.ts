import type { RoomSnapshot } from "./types";

/**
 * The room reaches the screen by several routes (push, refetch, the response to our own action) and they can
 * arrive out of order. Keep whichever snapshot is newest so an old response never overwrites a newer push.
 */
export function newerSnapshot(current: RoomSnapshot | null, next: RoomSnapshot): RoomSnapshot {
  if (!current || current.roomId !== next.roomId) return next;
  return next.sequence >= current.sequence ? next : current;
}

/** A failed action's message and the snapshot it happened on. */
export interface RoomError {
  message: string;
  sequence: number;
}

/**
 * An action error describes the room as it was when the action failed, so it stops being true as soon as a newer
 * snapshot arrives (a new round, a reconnect, the push that explains why the action lost a race).
 */
export function currentError(error: RoomError | null, snapshot: RoomSnapshot | null): string | null {
  if (!error || !snapshot) return null;
  return snapshot.sequence <= error.sequence ? error.message : null;
}
