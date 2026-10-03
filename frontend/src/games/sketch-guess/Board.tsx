import { useCallback, useEffect, useRef, useState, type PointerEvent } from "react";
import { FLUSH_MS, GRID, MAX_BATCH, MAX_STROKES, PALETTE, PEN_SIZES, SEGMENT_MS, isFull, movedEnough, paintStroke, toGrid } from "./strokes";
import type { Stroke } from "./types";

interface BoardProps {
  /** The strokes the server holds for this round. */
  strokes: Stroke[];
  /** Only the drawer gets tools and can draw. */
  canDraw: boolean;
  label: string;
  onStrokes: (batch: Stroke[]) => void;
  onUndo: () => void;
  onClear: () => void;
}

function useSizedCanvas(ref: React.RefObject<HTMLCanvasElement | null>, draw: (ctx: CanvasRenderingContext2D, scale: number) => void) {
  const drawRef = useRef(draw);
  useEffect(() => {
    drawRef.current = draw;
  });

  const repaint = useCallback(() => {
    const canvas = ref.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    const ratio = window.devicePixelRatio || 1;
    const width = canvas.clientWidth;
    if (width > 0 && canvas.width !== Math.round(width * ratio)) {
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(width * ratio);
    }
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    drawRef.current(ctx, (canvas.width || GRID) / GRID);
  }, [ref]);

  useEffect(() => {
    repaint();
    const canvas = ref.current;
    if (!canvas || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(repaint);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [ref, repaint]);

  return repaint;
}

const toolButton = (active: boolean) =>
  `grid min-h-11 min-w-11 place-items-center rounded-lg border-2 border-ink px-3 text-sm font-bold ${active ? "bg-ink text-paper" : "bg-card text-ink"}`;

/**
 * The shared drawing surface. Everyone sees the server's strokes; the drawer additionally sees their own unsent
 * strokes at once and sends them in small batches, slower than the server's minimum gap between canvas actions.
 */
export function Board({ strokes, canDraw, label, onStrokes, onUndo, onClear }: BoardProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [color, setColor] = useState(0);
  const [size, setSize] = useState<number>(PEN_SIZES[1].size);
  const pending = useRef<Stroke[]>([]);
  const inflight = useRef<Stroke[]>([]);
  const current = useRef<Stroke | null>(null);
  const lastSentAt = useRef(0);
  const segmentStartedAt = useRef(0);
  const [full, setFull] = useState(false);
  const flushTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [, bump] = useState(0);

  const repaint = useSizedCanvas(canvasRef, (ctx, scale) => {
    for (const stroke of [...strokes, ...inflight.current, ...pending.current]) paintStroke(ctx, stroke, scale);
    if (current.current) paintStroke(ctx, current.current, scale);
  });

  useEffect(() => {
    // A new server snapshot now carries what was in flight, so stop drawing it twice.
    inflight.current = [];
    repaint();
  }, [strokes, repaint]);

  const scheduleFlush = useCallback(() => {
    if (flushTimer.current) return;
    const send = () => {
      flushTimer.current = null;
      if (pending.current.length === 0) return;
      const batch = pending.current.splice(0, MAX_BATCH);
      inflight.current = [...inflight.current, ...batch];
      lastSentAt.current = Date.now();
      onStrokes(batch);
      if (pending.current.length > 0) flushTimer.current = setTimeout(send, FLUSH_MS);
    };
    flushTimer.current = setTimeout(send, Math.max(0, FLUSH_MS - (Date.now() - lastSentAt.current)));
  }, [onStrokes]);

  useEffect(
    () => () => {
      if (flushTimer.current) clearTimeout(flushTimer.current);
    },
    [],
  );

  function finishStroke() {
    const stroke = current.current;
    current.current = null;
    if (!stroke || stroke.points.length < 2) return;
    pending.current.push(stroke);
    scheduleFlush();
    repaint();
  }

  function pointFrom(event: PointerEvent<HTMLCanvasElement>): [number, number] {
    return toGrid(event.clientX, event.clientY, event.currentTarget.getBoundingClientRect());
  }

  function onDown(event: PointerEvent<HTMLCanvasElement>) {
    if (!canDraw) return;
    // Only the main button draws; a right-click would leave a stray mark and open the context menu.
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (strokes.length + inflight.current.length + pending.current.length >= MAX_STROKES) {
      setFull(true);
      return;
    }
    event.currentTarget.setPointerCapture?.(event.pointerId);
    segmentStartedAt.current = Date.now();
    const [x, y] = pointFrom(event);
    current.current = { color, size, points: [x, y] };
    repaint();
  }

  function onMove(event: PointerEvent<HTMLCanvasElement>) {
    const stroke = current.current;
    if (!stroke) return;
    const [x, y] = pointFrom(event);
    if (!movedEnough(stroke.points, x, y)) return;
    stroke.points.push(x, y);
    if (isFull(stroke) || Date.now() - segmentStartedAt.current >= SEGMENT_MS) {
      // Close this stroke and carry on from the same spot, so a long line is a few strokes end to end, and
      // guessers see it grow instead of waiting for the pointer to lift.
      finishStroke();
      current.current = { color: stroke.color, size: stroke.size, points: [x, y] };
      segmentStartedAt.current = Date.now();
    }
    repaint();
  }

  function undo() {
    if (pending.current.length > 0) {
      pending.current.pop();
      setFull(false);
      repaint();
      bump((n) => n + 1);
      return;
    }
    setFull(false);
    onUndo();
  }

  function clear() {
    pending.current = [];
    inflight.current = [];
    current.current = null;
    if (flushTimer.current) clearTimeout(flushTimer.current);
    flushTimer.current = null;
    setFull(false);
    repaint();
    onClear();
  }

  return (
    <div className="flex flex-col gap-3">
      <canvas
        ref={canvasRef}
        role="img"
        aria-label={label}
        onPointerDown={onDown}
        onPointerMove={onMove}
        onPointerUp={finishStroke}
        onPointerCancel={finishStroke}
        className={`aspect-square w-full max-w-xl self-center rounded-lg border-2 border-ink bg-white ${canDraw ? "cursor-crosshair touch-none" : ""}`}
      />
      {canDraw && full && (
        <p role="status" className="text-center font-bold text-tomato">
          The canvas is full. Undo or clear to keep drawing.
        </p>
      )}
      {canDraw && (
        <div className="flex flex-col gap-3" role="group" aria-label="Drawing tools">
          <div role="group" aria-label="Color" className="flex flex-wrap gap-2">
            {PALETTE.map((c, i) => (
              <button
                key={c.name}
                type="button"
                aria-label={c.name}
                aria-pressed={color === i}
                onClick={() => setColor(i)}
                style={{ backgroundColor: c.hex }}
                className={`size-11 rounded-full border-2 border-ink ${color === i ? "ring-4 ring-ink ring-offset-2" : ""}`}
              />
            ))}
          </div>
          <div role="group" aria-label="Pen size" className="flex flex-wrap gap-2">
            {PEN_SIZES.map((p) => (
              <button key={p.name} type="button" aria-pressed={size === p.size} onClick={() => setSize(p.size)} className={toolButton(size === p.size)}>
                {p.name}
              </button>
            ))}
            <button type="button" onClick={undo} className={toolButton(false)}>
              Undo
            </button>
            <button type="button" onClick={clear} className={toolButton(false)}>
              Clear
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
