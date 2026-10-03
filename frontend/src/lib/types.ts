export type SessionStatus = "Waiting" | "Active" | "Completed";

/** Room-level presence only; what a player has done *in* the game lives in the game's own payload. */
export interface PlayerView {
  name: string;
  online: boolean;
  /** False until the player has opened their invite link and set a PIN. */
  claimed: boolean;
}

export interface SessionView {
  id: string;
  number: number;
  status: SessionStatus;
  startedAt: string | null;
  endedAt: string | null;
}

export interface RoomSnapshot {
  roomId: string;
  roomSlug: string;
  roomTitle: string;
  hostPlayer: string;
  gameType: string;
  session: SessionView;
  players: PlayerView[];
  // Opaque here on purpose - each game module (see src/games) knows its own payload shape.
  gamePayload: unknown;
  /** Orders snapshots of one room; see newerSnapshot. */
  sequence: number;
}

export interface Session {
  token: string;
  player: string;
  roomSlug: string;
  isHost: boolean;
}

export interface RoomPreviewPlayer {
  name: string;
  claimed: boolean;
}

export interface RoomPreview {
  title: string;
  gameType: string;
  gamePreview: unknown;
  players: RoomPreviewPlayer[];
}

export interface PlayerInvite {
  player: string;
  inviteToken: string;
}

export interface CreateRoomResult {
  slug: string;
  invites: PlayerInvite[];
}
