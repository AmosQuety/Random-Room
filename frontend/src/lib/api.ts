import type { AppConfig, CreateRoomResult, PlayerInvite, RoomPreview, RoomSnapshot, Session } from "./types";

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
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const getConfig = () => request<AppConfig>("/api/config", { method: "GET" });

export const createRoom = (title: string, gameType: string, setup: unknown, players: string[], hostPlayer: string) =>
  request<CreateRoomResult>("/api/rooms", {
    method: "POST",
    body: JSON.stringify({ title, gameType, setup, players, hostPlayer }),
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

export const performAction = (token: string, action: string, payload?: unknown) =>
  request<RoomSnapshot>("/api/room/action", { method: "POST", body: JSON.stringify({ action, payload }) }, token);

/** Host only. Deletes the room and everything in it. Cannot be undone. */
export const deleteRoom = (token: string) => request<void>("/api/room", { method: "DELETE" }, token);

/** Host only. Empties a player's seat and returns the one-time link they use to set a new PIN. */
export const resetPin = (token: string, player: string) =>
  request<PlayerInvite>(`/api/room/players/${encodeURIComponent(player)}/reset-pin`, { method: "POST" }, token);

export const startSession = (token: string) => post("/api/room/session/start", token);
export const endSession = (token: string) => post("/api/room/session/end", token);
export const startNewSession = (token: string) => post("/api/room/session/new", token);
