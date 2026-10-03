import type { GlyphProps } from "../types";

export function NeverHaveIEverGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="9" y="20" width="30" height="22" rx="8" className="fill-accent stroke-ink" strokeWidth="3" />
      <path d="M14 22V9M20 20V5M26 20V7M32 22V11" className="stroke-ink" strokeWidth="5" strokeLinecap="round" />
      <path d="M36 24l7-6" className="stroke-ink" strokeWidth="5" strokeLinecap="round" />
    </svg>
  );
}
