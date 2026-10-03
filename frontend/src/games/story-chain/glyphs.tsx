import type { GlyphProps } from "../types";

export function OneWordGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="4" y="14" width="12" height="20" rx="3" className="fill-card stroke-ink" strokeWidth="3" />
      <rect x="18" y="14" width="12" height="20" rx="3" className="fill-accent stroke-ink" strokeWidth="3" />
      <rect x="32" y="14" width="12" height="20" rx="3" className="fill-card stroke-ink" strokeWidth="3" strokeDasharray="4 3" />
    </svg>
  );
}

export function FortunatelyGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <circle cx="17" cy="24" r="12" className="fill-accent stroke-ink" strokeWidth="3" />
      <path d="M12 26q5 6 10 0" className="stroke-ink" fill="none" strokeWidth="3" strokeLinecap="round" />
      <circle cx="33" cy="24" r="10" className="fill-card stroke-ink" strokeWidth="3" />
      <path d="M28 30q5-6 10 0" className="stroke-ink" fill="none" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
