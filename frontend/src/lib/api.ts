import type { RoomSnapshot, Session } from "./types";

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

export const join = (player: string, pin: string) =>
  request<Session>("/api/join", { method: "POST", body: JSON.stringify({ player, pin }) });

export const getRoom = (token: string) => request<RoomSnapshot>("/api/room", { method: "GET" }, token);

const post = (path: string, token: string) => request<RoomSnapshot>(path, { method: "POST" }, token);

export const triggerRandom = (token: string) => post("/api/room/trigger", token);
export const startRound = (token: string) => post("/api/room/round/start", token);
export const endRound = (token: string) => post("/api/room/round/end", token);
export const startNewRound = (token: string) => post("/api/room/round/new", token);
