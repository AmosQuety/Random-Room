import { useEffect, useRef, useState } from "react";
import type { TimerView } from "./types";

const TICK_MS = 250;
const RETRY_MS = 2000;
const MAX_ATTEMPTS = 3;
/** Fire just after zero so the server's own clock has certainly passed the deadline. */
const GRACE_MS = 200;

function secondsUntil(deadline: string, serverOffset: number): number {
  return Math.max(0, Math.ceil((Date.parse(deadline) - (Date.now() + serverOffset)) / 1000));
}

interface Reading {
  deadline: string;
  seconds: number;
}

/**
 * Counts down to a server deadline. The clock difference between this device and the server is measured when each
 * snapshot arrives, so a wrong device clock does not shorten or lengthen the round. Reaching zero only asks the
 * server to check (onExpire); the server decides whether the round is really over.
 */
export function useCountdown(timer: TimerView | null, active: boolean, onExpire: () => void): number | null {
  const deadline = timer?.deadlineAt ?? null;
  const serverNow = timer?.serverNow ?? null;
  const offset = useRef(0);
  const expire = useRef(onExpire);
  const [reading, setReading] = useState<Reading | null>(() =>
    deadline && serverNow ? { deadline, seconds: secondsUntil(deadline, Date.parse(serverNow) - Date.now()) } : null,
  );

  useEffect(() => {
    if (serverNow) offset.current = Date.parse(serverNow) - Date.now();
  }, [serverNow]);

  useEffect(() => {
    expire.current = onExpire;
  }, [onExpire]);

  useEffect(() => {
    if (!active || !deadline) return;
    const interval = setInterval(() => setReading({ deadline, seconds: secondsUntil(deadline, offset.current) }), TICK_MS);
    return () => clearInterval(interval);
  }, [active, deadline]);

  useEffect(() => {
    if (!active || !deadline) return;
    let attempts = 0;
    let retry: ReturnType<typeof setInterval> | undefined;
    const fire = () => {
      attempts += 1;
      expire.current();
      if (attempts >= MAX_ATTEMPTS) clearInterval(retry);
    };
    const wait = Math.max(0, Date.parse(deadline) - (Date.now() + offset.current)) + GRACE_MS;
    const first = setTimeout(() => {
      fire();
      retry = setInterval(fire, RETRY_MS);
    }, wait);
    return () => {
      clearTimeout(first);
      clearInterval(retry);
    };
  }, [active, deadline]);

  // A reading for an earlier round's deadline is stale: show nothing until the first tick of this one.
  return active && deadline && reading?.deadline === deadline ? reading.seconds : null;
}
