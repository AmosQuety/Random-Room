import { useCallback, useState } from "react";
import { JoinScreen } from "./components/JoinScreen";
import { RoomScreen } from "./components/RoomScreen";
import { clearSession, loadSession, saveSession } from "./lib/session";
import type { Session } from "./lib/types";

export default function App() {
  const [session, setSession] = useState<Session | null>(loadSession);

  const handleJoined = (next: Session) => {
    saveSession(next);
    setSession(next);
  };

  const handleLeave = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  return session ? <RoomScreen session={session} onLeave={handleLeave} /> : <JoinScreen onJoined={handleJoined} />;
}
