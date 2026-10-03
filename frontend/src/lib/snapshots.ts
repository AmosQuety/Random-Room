import type { RoomSnapshot } from "./types";

/**
 * The room reaches the screen by several routes (push, refetch, the response to our own action) and they can
 * arrive out of order. Keep whichever snapshot is newest so an old response never overwrites a newer push.
 */
export function newerSnapshot(current: RoomSnapshot | null, next: RoomSnapshot): RoomSnapshot {
  if (!current || current.roomId !== next.roomId) return next;
  return next.sequence >= current.sequence ? next : current;
}
