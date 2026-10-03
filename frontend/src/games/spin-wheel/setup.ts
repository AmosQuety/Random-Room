import { MAX_SEGMENTS, MIN_SEGMENTS, type WheelSetup } from "./types";

export const filledSegments = (segments: string[]): string[] => segments.map((s) => s.trim()).filter(Boolean);

export function isWheelSetupValid(setup: WheelSetup): boolean {
  const filled = filledSegments(setup.segments);
  const distinct = new Set(filled.map((s) => s.toLowerCase()));
  if (distinct.size !== filled.length || filled.length > MAX_SEGMENTS) return false;
  return setup.useBuiltIn || filled.length >= MIN_SEGMENTS;
}

export const toApiWheelSetup = (setup: WheelSetup) => ({
  segments: filledSegments(setup.segments),
  useBuiltIn: setup.useBuiltIn,
  spins: setup.spins,
});
