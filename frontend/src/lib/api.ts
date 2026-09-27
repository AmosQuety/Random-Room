import type { CreateRoomResult, RoomPreview, RoomSnapshot, Session } from "./types";

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, init: RequestInit, token?: string): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new ApiError(problem?.title ?? "Something went wrong.", response.status);
  }
  return response.json() as Promise<T>;
}

export const createRoom = (title: string, choices: string[], players: string[], hostPlayer: string) =>
  request<CreateRoomResult>("/api/rooms", {
    method: "POST",
    body: JSON.stringify({ title, choices, players, hostPlayer }),
  });

export const getRoomPreview = (slug: string) => request<RoomPreview>(`/api/rooms/${slug}`, { method: "GET" });

export const claimInvite = (slug: string, token: string, pin: string) =>
  request<{ player: string }>(`/api/rooms/${slug}/claim/${token}`, {
    method: "POST",
    body: JSON.stringify({ pin }),
  });

export const join = (roomSlug: string, player: string, pin: string) =>
  request<Session>("/api/join", { method: "POST", body: JSON.stringify({ roomSlug, player, pin }) });

export const getRoom = (token: string) => request<RoomSnapshot>("/api/room", { method: "GET" }, token);

const post = (path: string, token: string) => request<RoomSnapshot>(path, { method: "POST" }, token);

export const triggerRandom = (token: string) => post("/api/room/trigger", token);
export const startRound = (token: string) => post("/api/room/round/start", token);
export const endRound = (token: string) => post("/api/room/round/end", token);
export const startNewRound = (token: string) => post("/api/room/round/new", token);
