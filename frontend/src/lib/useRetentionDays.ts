import { useEffect, useState } from "react";
import { getConfig } from "./api";

let cached: number | null = null;

/**
 * How many days a room is kept without play, or null while unknown (or if the server cannot say). Fetched once per
 * page load. A notice that cannot be shown is better than one that is wrong, so a failure just shows nothing.
 */
export function useRetentionDays(): number | null {
  const [days, setDays] = useState<number | null>(cached);

  useEffect(() => {
    if (cached !== null) return;
    getConfig()
      .then((config) => {
        cached = config.retentionDays;
        setDays(config.retentionDays);
      })
      .catch(() => {});
  }, []);

  return days;
}

/** For tests, so one test's answer does not leak into the next. */
export function resetRetentionDaysCache() {
  cached = null;
}
