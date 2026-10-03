import type { GlyphProps } from "../types";

export function BingoGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="6" y="6" width="36" height="36" rx="5" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M18 6v36M30 6v36M6 18h36M6 30h36" className="stroke-ink" strokeWidth="2" />
      <circle cx="12" cy="12" r="4" className="fill-accent stroke-ink" strokeWidth="2" />
      <circle cx="24" cy="24" r="4" className="fill-accent stroke-ink" strokeWidth="2" />
      <circle cx="36" cy="36" r="4" className="fill-accent stroke-ink" strokeWidth="2" />
    </svg>
  );
}
