import type { GlyphProps } from "../types";

export function GuessWhoGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <circle cx="24" cy="24" r="19" className="fill-accent stroke-ink" strokeWidth="3" />
      <circle cx="24" cy="19" r="6" className="fill-card stroke-ink" strokeWidth="2.5" />
      <path d="M11 38c2-8 7-11 13-11s11 3 13 11" className="fill-card stroke-ink" strokeWidth="2.5" />
      <path d="M32 8c4 1 8 5 8 10" className="stroke-ink" fill="none" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
