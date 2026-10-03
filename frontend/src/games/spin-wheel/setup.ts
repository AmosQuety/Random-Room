import { normalizeAnswer } from "../../lib/text";
import { MAX_SEGMENTS, MIN_SEGMENTS, type WheelSetup } from "./types";

export const filledSegments = (segments: string[]): string[] => segments.map((s) => s.trim()).filter(Boolean);

/** Mirrors the server's checks (accents and punctuation ignored), so the host hears about a problem on the setup step. */
export function wheelSetupIssue(setup: WheelSetup): string | null {
  const filled = filledSegments(setup.segments);
  if (new Set(filled.map(normalizeAnswer)).size !== filled.length) return "Each wheel segment must be different.";
  if (filled.length > MAX_SEGMENTS) return `Use at most ${MAX_SEGMENTS} segments.`;
  if (!setup.useBuiltIn && filled.length < MIN_SEGMENTS) return `Add at least ${MIN_SEGMENTS} segments, or turn on the built-in ones.`;
  return null;
}

export const isWheelSetupValid = (setup: WheelSetup): boolean => wheelSetupIssue(setup) === null;

export const toApiWheelSetup = (setup: WheelSetup) => ({
  segments: filledSegments(setup.segments),
  useBuiltIn: setup.useBuiltIn,
  spins: setup.spins,
});
