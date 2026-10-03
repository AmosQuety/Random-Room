import type { GlyphProps } from "../types";

export function ThisOrThatGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <circle cx="15" cy="24" r="12" className="fill-accent stroke-ink" strokeWidth="3" />
      <rect x="25" y="12" width="21" height="24" rx="4" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M22 24h4" className="stroke-ink" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}
