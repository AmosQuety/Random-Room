export type SessionStatus = "Waiting" | "Active" | "Completed";

/** Room-level presence only; what a player has done *in* the game lives in the game's own payload. */
export interface PlayerView {
  name: string;
  online: boolean;
}

export interface SessionView {
  id: string;
  number: number;
  status: SessionStatus;
  startedAt: string | null;
  endedAt: string | null;
}

// --- Random Picker game payload (Room.gameType === "random-picker") ---

export interface RandomPickerPlayerState {
  hasTriggered: boolean;
  result: string | null;
}

export interface ActivityView {
  eventId: string;
  sessionId: string;
  sessionNumber: number;
  triggeredBy: string;
  result: string;
  timestamp: string;
}

export interface RandomPickerPayload {
  choices: string[];
  players: Record<string, RandomPickerPlayerState>;
  tally: { choice: string; count: number }[];
  activity: ActivityView[];
}

export interface RoomSnapshot {
  roomId: string;
  roomSlug: string;
  roomTitle: string;
  hostPlayer: string;
  gameType: string;
  session: SessionView;
  players: PlayerView[];
  // Only game type today is random-picker; once a second one exists this becomes a union keyed on gameType.
  gamePayload: RandomPickerPayload;
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
  choices: string[];
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
