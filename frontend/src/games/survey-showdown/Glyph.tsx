import type { GlyphProps } from "../types";

export function SurveyShowdownGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="4" y="6" width="40" height="36" rx="5" className="fill-accent stroke-ink" strokeWidth="3" />
      <rect x="10" y="12" width="28" height="7" rx="2" className="fill-card stroke-ink" strokeWidth="2.5" />
      <rect x="10" y="22" width="28" height="7" rx="2" className="fill-card stroke-ink" strokeWidth="2.5" />
      <rect x="10" y="32" width="18" height="6" rx="2" className="fill-card stroke-ink" strokeWidth="2.5" />
    </svg>
  );
}
