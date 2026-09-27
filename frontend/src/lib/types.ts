export type RoundStatus = "Waiting" | "Active" | "Completed";

export interface PlayerView {
  name: string;
  online: boolean;
  hasTriggered: boolean;
  result: string | null;
}

export interface ActivityView {
  eventId: string;
  roundId: string;
  roundNumber: number;
  triggeredBy: string;
  result: string;
  timestamp: string;
}

export interface RoomSnapshot {
  roomId: string;
  roomSlug: string;
  roomTitle: string;
  hostPlayer: string;
  choices: string[];
  round: { id: string; number: number; status: RoundStatus; startedAt: string | null; endedAt: string | null };
  players: PlayerView[];
  tally: { choice: string; count: number }[];
  activity: ActivityView[];
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
