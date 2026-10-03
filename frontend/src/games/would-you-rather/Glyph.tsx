import type { GlyphProps } from "../types";

export function WouldYouRatherGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <path d="M24 42V26M24 26L11 12M24 26l13-14" className="stroke-ink" fill="none" strokeWidth="4" strokeLinecap="round" />
      <circle cx="10" cy="10" r="7" className="fill-accent stroke-ink" strokeWidth="3" />
      <rect x="31" y="3.5" width="14" height="14" rx="3" className="fill-card stroke-ink" strokeWidth="3" />
      <circle cx="24" cy="41" r="3.5" className="fill-ink" />
    </svg>
  );
}
