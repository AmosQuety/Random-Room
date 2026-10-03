import type { Stroke } from "./types";

export const GRID = 1000;
/** Matches the server's caps, so a stroke the client sends is never refused for size. */
export const MAX_POINTS_PER_STROKE = 200;
export const MAX_BATCH = 20;
/** The most strokes the server keeps for one round. */
export const MAX_STROKES = 400;
/** The most points the server keeps for one round, across all strokes. */
export const MAX_TOTAL_POINTS = 6000;
/** While the pointer is down, the line so far is cut into a stroke this often so guessers see it as it is drawn. */
export const SEGMENT_MS = 500;
/** The server refuses canvas actions closer than 100ms; batching slower than that keeps a fast drawer accepted. */
export const FLUSH_MS = 160;
const MIN_STEP = 4;

export const PALETTE = [
  { name: "Black", hex: "#1a1a1a" },
  { name: "Red", hex: "#d9382b" },
  { name: "Orange", hex: "#f08a24" },
  { name: "Yellow", hex: "#f2c230" },
  { name: "Green", hex: "#2f9e57" },
  { name: "Blue", hex: "#2f6fd6" },
  { name: "Purple", hex: "#7a3fb0" },
  { name: "Brown", hex: "#7a4a2b" },
] as const;

export const PEN_SIZES = [
  { name: "Fine", size: 4 },
  { name: "Medium", size: 9 },
  { name: "Thick", size: 16 },
  { name: "Marker", size: 24 },
] as const;

const clampGrid = (n: number) => Math.min(GRID, Math.max(0, Math.round(n)));

/** Maps a pointer position inside a box to integer grid coordinates. */
export function toGrid(clientX: number, clientY: number, box: { left: number; top: number; width: number; height: number }): [number, number] {
  if (box.width <= 0 || box.height <= 0) return [0, 0];
  return [clampGrid(((clientX - box.left) / box.width) * GRID), clampGrid(((clientY - box.top) / box.height) * GRID)];
}

/** True when the point is far enough from the last one to be worth recording. */
export function movedEnough(points: number[], x: number, y: number): boolean {
  const n = points.length;
  if (n < 2) return true;
  return Math.abs(points[n - 2] - x) + Math.abs(points[n - 1] - y) >= MIN_STEP;
}

export const pointCount = (strokes: readonly Stroke[]): number => strokes.reduce((sum, s) => sum + s.points.length / 2, 0);

/** True when another full-length stroke could push the round past the server's stroke or point cap. */
export const canvasIsFull = (strokes: readonly Stroke[]): boolean =>
  strokes.length >= MAX_STROKES || pointCount(strokes) > MAX_TOTAL_POINTS - MAX_POINTS_PER_STROKE;

export const isFull = (stroke: Stroke): boolean => stroke.points.length / 2 >= MAX_POINTS_PER_STROKE;

export function paintStroke(ctx: CanvasRenderingContext2D, stroke: Stroke, scale: number): void {
  const { points } = stroke;
  if (points.length < 2) return;
  ctx.strokeStyle = PALETTE[stroke.color]?.hex ?? PALETTE[0].hex;
  ctx.fillStyle = ctx.strokeStyle;
  ctx.lineWidth = stroke.size * scale;
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  if (points.length === 2) {
    ctx.beginPath();
    ctx.arc(points[0] * scale, points[1] * scale, (stroke.size * scale) / 2, 0, Math.PI * 2);
    ctx.fill();
    return;
  }
  ctx.beginPath();
  ctx.moveTo(points[0] * scale, points[1] * scale);
  for (let i = 2; i < points.length; i += 2) ctx.lineTo(points[i] * scale, points[i + 1] * scale);
  ctx.stroke();
}
