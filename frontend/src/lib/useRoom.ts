import { useCallback, useEffect, useState } from "react";
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { ApiError, getRoom } from "./api";
import type { RoomSnapshot } from "./types";

export type ConnectionStatus = "connecting" | "live" | "reconnecting";

interface UseRoom {
  snapshot: RoomSnapshot | null;
  connection: ConnectionStatus;
  applySnapshot: (snapshot: RoomSnapshot) => void;
}

/** Keeps the room in sync: push updates over SignalR, with a full refetch after every (re)connect. */
export function useRoom(token: string, onUnauthorized: () => void): UseRoom {
  const [snapshot, setSnapshot] = useState<RoomSnapshot | null>(null);
  const [connection, setConnection] = useState<ConnectionStatus>("connecting");

  const refetch = useCallback(async () => {
    try {
      setSnapshot(await getRoom(token));
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) onUnauthorized();
    }
  }, [token, onUnauthorized]);

  useEffect(() => {
    const hub = new HubConnectionBuilder()
      .withUrl("/hubs/room", { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    hub.on("roomChanged", (next: RoomSnapshot) => setSnapshot(next));
    hub.onreconnecting(() => setConnection("reconnecting"));
    hub.onreconnected(() => {
      setConnection("live");
      void refetch();
    });
    hub.onclose(() => setConnection("reconnecting"));

    void refetch();
    hub
      .start()
      .then(() => setConnection("live"))
      .catch(() => setConnection("reconnecting"));

    return () => {
      hub.off("roomChanged");
      if (hub.state !== HubConnectionState.Disconnected) void hub.stop();
    };
  }, [token, refetch]);

  return { snapshot, connection, applySnapshot: setSnapshot };
}
