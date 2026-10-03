import type { CSSProperties } from "react";
import { rotationFor } from "./wheelMath";

const SIZE = 300;
const CENTER = SIZE / 2;
const RADIUS = 140;
const LABEL_LIMIT = 14;

function point(angle: number, radius: number): [number, number] {
  const rad = ((angle - 90) * Math.PI) / 180;
  return [CENTER + radius * Math.cos(rad), CENTER + radius * Math.sin(rad)];
}

function wedge(index: number, count: number): string {
  const step = 360 / count;
  const [x1, y1] = point(index * step, RADIUS);
  const [x2, y2] = point((index + 1) * step, RADIUS);
  return `M${CENTER} ${CENTER}L${x1} ${y1}A${RADIUS} ${RADIUS} 0 0 1 ${x2} ${y2}Z`;
}

const shorten = (text: string) => (text.length > LABEL_LIMIT ? `${text.slice(0, LABEL_LIMIT - 1)}…` : text);

interface Props {
  segments: string[];
  /** The server's chosen segment, or null before the spin. The disc only animates towards it. */
  target: number | null;
}

/**
 * The wheel is a picture of a decision the server already made. Remount it per turn (key) so it starts at
 * rest and the CSS transition plays when the result arrives; mounted with a result (a refresh) it simply rests there.
 */
export function Wheel({ segments, target }: Props) {
  const style: CSSProperties = { transform: `rotate(${target === null ? 0 : rotationFor(target, segments.length)}deg)` };
  const step = 360 / segments.length;

  return (
    <div className="relative mx-auto aspect-square w-full max-w-[20rem]">
      <div aria-hidden="true" className="absolute left-1/2 top-0 z-10 -translate-x-1/2 -translate-y-1">
        <svg viewBox="0 0 24 28" className="h-7 w-6">
          <path d="M12 26L2 4h20z" className="fill-tomato stroke-ink" strokeWidth="2.5" strokeLinejoin="round" />
        </svg>
      </div>
      <svg
        viewBox={`0 0 ${SIZE} ${SIZE}`}
        role="img"
        aria-label={`Wheel with ${segments.length} segments`}
        style={style}
        className="size-full origin-center transition-transform duration-[4000ms] ease-[cubic-bezier(0.12,0.7,0.1,1)] motion-reduce:transition-none"
      >
        {segments.map((segment, i) => {
          const [tx, ty] = point((i + 0.5) * step, RADIUS * 0.62);
          return (
            <g key={i}>
              <path d={wedge(i, segments.length)} className={`stroke-ink ${i % 2 === 0 ? "fill-card" : "fill-accent-soft"}`} strokeWidth="2" />
              <text
                x={tx}
                y={ty}
                textAnchor="middle"
                dominantBaseline="middle"
                transform={`rotate(${(i + 0.5) * step - 90} ${tx} ${ty})`}
                className="fill-ink text-[11px] font-bold"
              >
                {shorten(segment)}
              </text>
            </g>
          );
        })}
        <circle cx={CENTER} cy={CENTER} r="12" className="fill-ink" />
      </svg>
    </div>
  );
}
