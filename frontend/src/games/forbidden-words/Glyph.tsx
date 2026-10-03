import type { GlyphProps } from "../types";

export function ForbiddenGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="8" y="5" width="32" height="38" rx="4" className="fill-card stroke-ink" strokeWidth="3" />
      <rect x="13" y="10" width="22" height="8" rx="2" className="fill-accent stroke-ink" strokeWidth="2" />
      <path d="M14 26h20M14 32h20M14 38h12" className="stroke-ink" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="36" cy="34" r="9" className="fill-card stroke-tomato" strokeWidth="3.5" />
      <path d="M30 40l12-12" className="stroke-tomato" strokeWidth="3.5" strokeLinecap="round" />
    </svg>
  );
}
