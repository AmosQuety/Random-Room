import { useCallback, useState } from "react";
import { ClaimInviteScreen } from "./components/ClaimInviteScreen";
import { HomeScreen } from "./components/HomeScreen";
import { JoinScreen } from "./components/JoinScreen";
import { RoomScreen } from "./components/RoomScreen";
import { usePath } from "./lib/router";
import { clearSession, loadSession, saveSession } from "./lib/session";
import type { SessionEnd } from "./lib/useRoom";
import type { Session } from "./lib/types";

const SESSION_END_NOTICE: Record<SessionEnd, string> = {
  expired: "Your sign-in has ended, so you were signed out. Pick your name and enter your PIN to get back in.",
  reset: "The host reset your seat, so you were signed out. Ask them for your new invite link, open it, and choose a new PIN.",
};

export default function App() {
  const path = usePath();
  const [session, setSession] = useState<Session | null>(loadSession);
  // Why the player is looking at the join screen, when it was not their own choice.
  const [notice, setNotice] = useState<string | null>(null);

  const handleJoined = (next: Session) => {
    saveSession(next);
    setNotice(null);
    setSession(next);
  };

  const handleLeave = useCallback(() => {
    clearSession();
    setNotice(null);
    setSession(null);
  }, []);

  const handleSessionExpired = useCallback((reason: SessionEnd) => {
    clearSession();
    setNotice(SESSION_END_NOTICE[reason]);
    setSession(null);
  }, []);

  const claimMatch = path.match(/^\/room\/([^/]+)\/claim\/([^/]+)\/?$/);
  if (claimMatch) return <ClaimInviteScreen slug={claimMatch[1]} token={claimMatch[2]} onJoined={handleJoined} />;

  const roomMatch = path.match(/^\/room\/([^/]+)(?:\/join)?\/?$/);
  if (roomMatch) {
    const slug = roomMatch[1];
    if (session && session.roomSlug === slug) return <RoomScreen session={session} onLeave={handleLeave} onSessionExpired={handleSessionExpired} />;
    return <JoinScreen slug={slug} onJoined={handleJoined} notice={notice} />;
  }

  return <HomeScreen />;
}
