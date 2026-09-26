export type RoundStatus = "Waiting" | "Active" | "Completed";

export interface Choice {
  name: string;
  age: number;
}

export interface PlayerView {
  name: string;
  online: boolean;
  hasTriggered: boolean;
  result: string | null;
}

export interface ActivityView {
  eventId: string;
  roomId: string;
  roundId: string;
  roundNumber: number;
  triggeredBy: string;
  result: string;
  timestamp: string;
}

export interface RoomSnapshot {
  roomId: string;
  roomName: string;
  hostPlayer: string;
  choices: Choice[];
  round: { id: string; number: number; status: RoundStatus; startedAt: string | null; endedAt: string | null };
  players: PlayerView[];
  tally: { choice: string; count: number }[];
  activity: ActivityView[];
}

export interface Session {
  token: string;
  player: string;
  isHost: boolean;
}
