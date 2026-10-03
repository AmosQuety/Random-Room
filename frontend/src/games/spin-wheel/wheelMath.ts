const FULL_TURNS = 5;

/** Rotation that brings the middle of a segment under the pointer at the top, after several full turns. */
export function rotationFor(index: number, count: number): number {
  const step = 360 / count;
  return FULL_TURNS * 360 + (360 - (index + 0.5) * step);
}
