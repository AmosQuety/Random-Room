import { useCallback, useState } from "react";
import { ClaimInviteScreen } from "./components/ClaimInviteScreen";
import { HomeScreen } from "./components/HomeScreen";
import { JoinScreen } from "./components/JoinScreen";
import { RoomScreen } from "./components/RoomScreen";
import { usePath } from "./lib/router";
import { clearSession, loadSession, saveSession } from "./lib/session";
import type { Session } from "./lib/types";

export default function App() {
  const path = usePath();
  const [session, setSession] = useState<Session | null>(loadSession);

  const handleJoined = (next: Session) => {
    saveSession(next);
    setSession(next);
  };

  const handleLeave = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  const claimMatch = path.match(/^\/room\/([^/]+)\/claim\/([^/]+)\/?$/);
  if (claimMatch) return <ClaimInviteScreen slug={claimMatch[1]} token={claimMatch[2]} onJoined={handleJoined} />;

  const roomMatch = path.match(/^\/room\/([^/]+)(?:\/join)?\/?$/);
  if (roomMatch) {
    const slug = roomMatch[1];
    if (session && session.roomSlug === slug) return <RoomScreen session={session} onLeave={handleLeave} />;
    return <JoinScreen slug={slug} onJoined={handleJoined} />;
  }

  return <HomeScreen />;
}
