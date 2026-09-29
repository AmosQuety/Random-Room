import type { GlyphProps } from "../types";

export function WordSpiesGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="5" y="5" width="38" height="38" rx="5" className="fill-card stroke-ink" strokeWidth="3" />
      <rect x="9" y="9" width="8" height="8" rx="1.5" className="fill-tomato stroke-ink" strokeWidth="2" />
      <rect x="20" y="9" width="8" height="8" rx="1.5" className="fill-sky stroke-ink" strokeWidth="2" />
      <rect x="31" y="9" width="8" height="8" rx="1.5" className="fill-paper stroke-ink" strokeWidth="2" />
      <rect x="9" y="20" width="8" height="8" rx="1.5" className="fill-sky stroke-ink" strokeWidth="2" />
      <rect x="20" y="20" width="8" height="8" rx="1.5" className="fill-ink" />
      <rect x="31" y="20" width="8" height="8" rx="1.5" className="fill-tomato stroke-ink" strokeWidth="2" />
      <rect x="9" y="31" width="8" height="8" rx="1.5" className="fill-paper stroke-ink" strokeWidth="2" />
      <rect x="20" y="31" width="8" height="8" rx="1.5" className="fill-tomato stroke-ink" strokeWidth="2" />
      <rect x="31" y="31" width="8" height="8" rx="1.5" className="fill-sky stroke-ink" strokeWidth="2" />
    </svg>
  );
}
