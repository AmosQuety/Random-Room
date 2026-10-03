import type { GlyphProps } from "../types";

export function MostLikelyToGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <path d="M6 32l3-20 10 10 5-14 5 14 10-10 3 20z" className="fill-accent stroke-ink" strokeWidth="3" strokeLinejoin="round" />
      <rect x="6" y="34" width="36" height="8" rx="2" className="fill-card stroke-ink" strokeWidth="3" />
    </svg>
  );
}
