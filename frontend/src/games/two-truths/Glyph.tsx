import type { GlyphProps } from "../types";

export function TwoTruthsGlyph({ className = "size-10" }: GlyphProps) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <rect x="4" y="5" width="40" height="10" rx="3" className="fill-card stroke-ink" strokeWidth="3" />
      <rect x="4" y="19" width="40" height="10" rx="3" className="fill-card stroke-ink" strokeWidth="3" />
      <rect x="4" y="33" width="40" height="10" rx="3" className="fill-accent stroke-ink" strokeWidth="3" />
      <path d="M12 38h14" className="stroke-on-accent" strokeWidth="3" strokeLinecap="round" strokeDasharray="1 5" />
    </svg>
  );
}
