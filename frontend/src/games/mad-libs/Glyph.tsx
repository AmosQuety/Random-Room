import type { GlyphProps } from "../types";

export function MadLibsGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="7" y="5" width="34" height="38" rx="4" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M13 15h10M13 25h6M13 35h9" className="stroke-ink" strokeWidth="3" strokeLinecap="round" />
      <path d="M26 15h9M23 25h12M26 35h9" className="stroke-accent" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}
