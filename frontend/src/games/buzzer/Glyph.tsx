import type { GlyphProps } from "../types";

export function BuzzerGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="6" y="30" width="36" height="12" rx="4" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M12 30a12 12 0 0124 0z" className="fill-accent stroke-ink" strokeWidth="3" strokeLinejoin="round" />
      <path d="M24 6v5M10 11l3 3.5M38 11l-3 3.5" className="stroke-ink" fill="none" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
