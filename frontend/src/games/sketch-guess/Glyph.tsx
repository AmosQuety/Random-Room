import type { GlyphProps } from "../types";

export function SketchGuessGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="5" y="7" width="32" height="26" rx="3" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M11 26c4-10 8 4 12-6s5 2 9-4" className="stroke-accent" strokeWidth="3.5" strokeLinecap="round" fill="none" />
      <path d="M40 20l4 4-14 16-6 2 2-6z" className="fill-mustard stroke-ink" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M11 40h10" className="stroke-ink" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
